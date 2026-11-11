import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { onStepOutput, onWorkItemChanged } from '@/api/live'
import * as stepsApi from '@/api/steps'
import type { StepRun, WorkItemDetails } from '@/api/types'
import { fetchWorkItem } from '@/api/workItems'

import { useNotifications } from './notifications'
import { useWorkItems } from './workItems'

/** Otwarte zadanie: szczegóły, decyzje o krokach i wyjście kroków na żywo. */
export const useWorkItemDetail = defineStore('workItemDetail', () => {
  const item = ref<WorkItemDetails | null>(null)
  const liveOutput = ref<Record<string, string>>({})
  const busy = ref(false)

  const notifications = useNotifications()
  const workItems = useWorkItems()

  const currentStep = computed(() => item.value?.steps.find((step) => !isFinished(step)) ?? null)
  const notStarted = computed(() => item.value?.steps.every((step) => step.status === 'Pending') ?? false)

  // Backend wysyła cały aktualny obraz zadania po każdej zmianie.
  onWorkItemChanged((changed) => {
    workItems.replace(changed.summary)
    if (changed.summary.id === item.value?.summary.id) item.value = changed
  })

  onStepOutput((workItemId, stepRunId, text) => {
    if (workItemId !== item.value?.summary.id) return
    liveOutput.value[stepRunId] = (liveOutput.value[stepRunId] ?? '') + text
  })

  async function load(id: string) {
    item.value = null
    liveOutput.value = {}
    item.value = await fetchWorkItem(id)
  }

  /** Wyjście kroku: w trakcie działania to, co przyszło na żywo; potem zapisany log. */
  const outputOf = (step: StepRun) => {
    const live = liveOutput.value[step.id] ?? ''
    return live.length > step.log.length ? live : step.log
  }

  /** Wykonuje akcję na API, podmienia stan zadania i pokazuje błąd, jeśli się nie uda. */
  async function run(action: () => Promise<WorkItemDetails | void>) {
    busy.value = true
    try {
      const updated = await action()
      if (updated) item.value = updated
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

  const approve = (step: StepRun) =>
    run(() => {
      delete liveOutput.value[step.id]
      return stepsApi.approveStep(step.id)
    })

  const retry = (step: StepRun, remark: string) => run(() => stepsApi.retryStep(step.id, remark.trim() || null))

  return { item, busy, currentStep, notStarted, load, outputOf, start, edit, approve, skip, retry, cancel }
})

const isFinished = (step: StepRun) => step.status === 'Succeeded' || step.status === 'Skipped'
