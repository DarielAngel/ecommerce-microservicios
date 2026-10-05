const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function formatMoney(value) {
  if (typeof value !== 'number' || Number.isNaN(value)) return ''
  return money.format(value)
}

export function formatDate(value) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleDateString('es', { year: 'numeric', month: 'short', day: 'numeric' })
}
