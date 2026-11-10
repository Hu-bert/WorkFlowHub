import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface Notification {
  text: string
  color: 'success' | 'error' | 'info' | 'warning'
}

/** Krótkie komunikaty dla użytkownika (pasek na dole ekranu). */
export const useNotifications = defineStore('notifications', () => {
  const current = ref<Notification | null>(null)

  const show = (text: string, color: Notification['color'] = 'info') => (current.value = { text, color })
  const success = (text: string) => show(text, 'success')
  const error = (problem: unknown) => show(problem instanceof Error ? problem.message : String(problem), 'error')
  const clear = () => (current.value = null)

  return { current, show, success, error, clear }
})
