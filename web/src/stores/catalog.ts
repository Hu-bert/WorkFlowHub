import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { fetchBoard, fetchFlows, fetchRepositories, reloadFlows, reloadRepositories } from '@/api/catalog'
import { fetchModels, type ModelsInfo } from '@/api/tokens'
import type { BoardColumn, CodeRepository, FlowDefinition, FlowProblem } from '@/api/types'

/** Dane słownikowe: typy zadań (flow), katalog repo, kolumny tablicy, routing modeli. Wczytywane raz. */
export const useCatalog = defineStore('catalog', () => {
  const flows = ref<FlowDefinition[]>([])
  const flowProblems = ref<FlowProblem[]>([])
  const repositories = ref<CodeRepository[]>([])
  const columns = ref<BoardColumn[]>([])
  const models = ref<ModelsInfo | null>(null)
  const loaded = ref(false)

  const flowNames = computed(() => new Map(flows.value.map((flow) => [flow.type, flow.name])))
  const flowName = (type: string) => flowNames.value.get(type) ?? type

  async function load(force = false) {
    if (loaded.value && !force) return

    const [flowsResponse, repositoryList, board, modelsInfo] = await Promise.all([
      fetchFlows(),
      fetchRepositories(),
      fetchBoard(),
      fetchModels(),
    ])
    flows.value = flowsResponse.flows
    flowProblems.value = flowsResponse.problems
    repositories.value = repositoryList
    columns.value = board
    models.value = modelsInfo
    loaded.value = true
  }

  /** Czyta YAML-e z dysku od nowa (po ręcznej edycji plików). */
  async function reloadFromDisk() {
    const [flowsResponse, repositoryList] = await Promise.all([reloadFlows(), reloadRepositories()])
    flows.value = flowsResponse.flows
    flowProblems.value = flowsResponse.problems
    repositories.value = repositoryList
  }

  return { flows, flowProblems, repositories, columns, models, loaded, flowName, load, reloadFromDisk }
})
