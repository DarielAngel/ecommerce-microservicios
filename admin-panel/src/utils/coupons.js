// Conversión entre el formulario del panel y la API de cupones, y textos para la tabla.
// Todo aquí es puro (sin red ni Vue) para poder probarlo aislado.

export function emptyCouponForm() {
  return {
    code: '',
    description: '',
    type: 'Percentage',
    value: '',
    maxDiscountAmount: '',
    minimumSubtotal: '',
    startsAt: '', // valor de un <input type="datetime-local">, en hora LOCAL
    endsAt: '',
    usageLimit: '',
    oncePerCustomer: false,
    isActive: true
  }
}

const toNumberOrNull = (v) => (v === '' || v === null || v === undefined ? null : Number(v))

// "2026-11-28T00:00" (hora local del Admin) → ISO en UTC, que es lo que guarda la API.
function localInputToUtcIso(value) {
  return value ? new Date(value).toISOString() : null
}

// ISO en UTC → "2026-11-28T00:00" en la hora local del navegador, para el <input>.
function utcIsoToLocalInput(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function toPayload(form) {
  return {
    code: form.code.trim().toUpperCase(),
    description: form.description.trim(),
    type: form.type,
    value: Number(form.value),
    // El tope solo tiene sentido en porcentajes; la API rechaza un tope en montos fijos.
    maxDiscountAmount: form.type === 'Percentage' ? toNumberOrNull(form.maxDiscountAmount) : null,
    minimumSubtotal: toNumberOrNull(form.minimumSubtotal) ?? 0,
    startsAtUtc: localInputToUtcIso(form.startsAt),
    endsAtUtc: localInputToUtcIso(form.endsAt),
    usageLimit: toNumberOrNull(form.usageLimit),
    oncePerCustomer: !!form.oncePerCustomer,
    isActive: !!form.isActive
  }
}

export function fromCoupon(coupon) {
  return {
    code: coupon.code,
    description: coupon.description,
    type: coupon.type,
    value: coupon.value,
    maxDiscountAmount: coupon.maxDiscountAmount ?? '',
    minimumSubtotal: coupon.minimumSubtotal || '',
    startsAt: utcIsoToLocalInput(coupon.startsAtUtc),
    endsAt: utcIsoToLocalInput(coupon.endsAtUtc),
    usageLimit: coupon.usageLimit ?? '',
    oncePerCustomer: coupon.oncePerCustomer,
    isActive: coupon.isActive
  }
}

const money = (n) => `$${Number(n).toFixed(2)}`

/// "15 % (máx. $50.00)" o "$10.00".
export function describeDiscount(coupon) {
  if (coupon.type === 'FixedAmount') return money(coupon.value)
  const pct = `${Number(coupon.value)} %`
  return coupon.maxDiscountAmount ? `${pct} (máx. ${money(coupon.maxDiscountAmount)})` : pct
}

/// Estado tal como lo vería un cliente en este momento, de mayor a menor prioridad.
export function couponStatus(coupon, now = new Date()) {
  if (!coupon.isActive) return { key: 'inactive', label: 'Pausado' }
  if (coupon.endsAtUtc && now >= new Date(coupon.endsAtUtc)) return { key: 'expired', label: 'Vencido' }
  if (coupon.startsAtUtc && now < new Date(coupon.startsAtUtc)) return { key: 'scheduled', label: 'Programado' }
  const used = (coupon.timesUsed ?? 0) + (coupon.activeReservations ?? 0)
  if (coupon.usageLimit && used >= coupon.usageLimit) return { key: 'exhausted', label: 'Agotado' }
  return { key: 'active', label: 'Activo' }
}

export function usageLabel(coupon) {
  return coupon.usageLimit ? `${coupon.timesUsed} / ${coupon.usageLimit}` : `${coupon.timesUsed}`
}
