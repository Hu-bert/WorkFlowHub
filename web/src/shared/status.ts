/** Jak pokazać status w UI: zawsze kolor + ikona + tekst (nie tylko kolor). */
export interface StatusPresentation {
  label: string
  icon: string
  color: 'success' | 'info' | 'warning' | 'error' | 'primary' | 'grey'
}
