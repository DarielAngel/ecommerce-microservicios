// Ayudas de la libreta de direcciones (Fase 5).

/** Campos del formulario, vacíos (quién recibe se puede precargar con el nombre del cliente). */
export function emptyAddress(recipientName = '') {
  return {
    label: 'Casa',
    recipientName,
    phone: '',
    street: '',
    details: '',
    city: '',
    region: '',
    postalCode: '',
    country: '',
    makeDefault: false
  }
}

/** Copia editable de una dirección guardada. */
export function toForm(address) {
  const form = emptyAddress()
  for (const key of Object.keys(form)) {
    if (key === 'makeDefault') continue
    form[key] = address[key] ?? ''
  }
  form.makeDefault = Boolean(address.isDefault)
  return form
}

const clean = (v) => (typeof v === 'string' ? v.trim() : '')

/** ¿Están los campos obligatorios? (el servidor valida de nuevo; esto evita un viaje inútil). */
export function isComplete(form) {
  return ['recipientName', 'street', 'city', 'country'].every((k) => clean(form[k]).length > 0)
}

/**
 * La dirección en una línea, igual que Address.Format() en el servicio Users. Se usa cuando el
 * cliente paga con una dirección que NO quiere guardar.
 */
export function formatAddress(form) {
  const opt = (k) => clean(form[k]) || null
  const parts = [clean(form.recipientName), clean(form.street)]
  if (opt('details')) parts.push(opt('details'))
  parts.push([opt('postalCode'), clean(form.city)].filter(Boolean).join(' '))
  const region = opt('region')
  if (region && region.toLowerCase() !== clean(form.city).toLowerCase()) parts.push(region)
  parts.push(clean(form.country))
  const text = parts.join(', ')
  return opt('phone') ? `${text} · Tel. ${opt('phone')}` : text
}

/**
 * Texto de error para mostrar: si el servidor devolvió errores por campo (400 de validación),
 * los lista; si no, el mensaje general.
 */
export function errorText(err) {
  const errors = err?.body?.errors
  if (errors && typeof errors === 'object') {
    const messages = Object.values(errors).flat().filter(Boolean)
    if (messages.length) return [...new Set(messages)].join(' ')
  }
  return err?.message || 'Ocurrió un error. Intenta de nuevo.'
}
