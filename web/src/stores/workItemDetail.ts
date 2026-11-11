import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { decidePermission, fetchPermissions } from '@/api/agent'
import { onPermissionRequested, onPermissionResolved, onStepOutput, onWorkItemChanged } from '@/api/live'
import * as stepsApi from '@/api/steps'
import type { PermissionRequest, StepRun, WorkItemDetails } from '@/api/types'
import { fetchWorkItem } from '@/api/workItems'

import { useNotifications } from './notifications'
import { useWorkItems } from './workItems'

/** Otwarte zadanie: szczegóły, decyzje o krokach, prośby o zgodę i wyjście kroków na żywo. */
export const useWorkItemDetail = defineStore('workItemDetail', () => {
  const item = ref<WorkItemDetails | null>(null)
  const liveOutput = ref<Record<string, string>>({})
  const permissions = ref<PermissionRequest[]>([])
  const busy = ref(false)

  const notifications = useNotifications()
  const workItems = useWorkItems()

  const currentStep = computed(() => item.value?.steps.find((step) => !isFinished(step)) ?? null)
  const notStarted = computed(() => item.value?.steps.every((step) => step.status === 'Pending') ?? false)
  const isOpen = (workItemId: string) => workItemId === item.value?.summary.id

  // Backend wysyła cały aktualny obraz zadania po każdej zmianie.
  let liveUpdates = 0

  onWorkItemChanged((changed) => {
    workItems.replace(changed.summary)
    if (isOpen(changed.summary.id)) {
      item.value = changed
      liveUpdates++
    }
  })

  onStepOutput((workItemId, stepRunId, text) => {
    if (!isOpen(workItemId)) return
    liveOutput.value[stepRunId] = (liveOutput.value[stepRunId] ?? '') + text
  })

  onPermissionRequested((request) => {
    if (isOpen(request.workItemId)) permissions.value.push(request)
  })

  onPermissionResolved((request) => {
    permissions.value = permissions.value.filter((pending) => pending.id !== request.id)
  })

  async function load(id: string) {
    item.value = null
    liveOutput.value = {}
    permissions.value = []

    const [details, pending] = await Promise.all([fetchWorkItem(id), fetchPermissions(id)])
    item.value = details
    permissions.value = pending
  }

  /** Wyjście kroku: w trakcie działania to, co przyszło na żywo; potem zapisany log. */
  const outputOf = (step: StepRun) => {
    const live = liveOutput.value[step.id] ?? ''
    return live.length > step.log.length ? live : step.log
  }

  const permissionFor = (step: StepRun) => permissions.value.find((request) => request.stepRunId === step.id) ?? null

  /** Wykonuje akcję na API, podmienia stan zadania i pokazuje błąd, jeśli się nie uda. */
  async function run(action: () => Promise<WorkItemDetails | void>) {
    busy.value = true
    const liveUpdatesBefore = liveUpdates
    try {
      const updated = await action()
      // Odpowiedź HTTP bywa starsza niż stan, który w międzyczasie przyszedł na żywo — wtedy ją pomijamy.
      if (updated && liveUpdates === liveUpdatesBefore) item.value = updated
    } catch (error) {
      notifications.error(error)
    } finally {
      busy.value = false
    }
  }

  const start = () => run(() => stepsApi.startFlow(item.value!.summary.id))
  const edit = (step: StepRun, request: stepsApi.EditProposalRequest) => run(() => stepsApi.editProposal(step.id, request))
  const skip = (step: StepRun) => run(() => stepsApi.skipStep(step.id))
  const cancel = (step: StepRun) => run(() => stepsApi.cancelStep(step.id))
  const decide = (request: PermissionRequest, allow: boolean) => run(() => decidePermission(request.id, allow))

  const approve = (step: StepRun) =>
    run(() => {
      delete liveOutput.value[step.id]
      return stepsApi.approveStep(step.id)
    })

  const followUp = (step: StepRun, remark: string, repo: string | null) =>
    run(() => stepsApi.followUpStep(step.id, remark.trim(), repo))

  const retry = (step: StepRun, remark: string) => run(() => stepsApi.retryStep(step.id, remark.trim() || null))

  return {
    item,
    busy,
    currentStep,
    notStarted,
    load,
    outputOf,
    permissionFor,
    start,
    edit,
    approve,
    skip,
    retry,
    followUp,
    cancel,
    decide,
  }
})

const isFinished = (step: StepRun) => step.status === 'Succeeded' || step.status === 'Skipped'
