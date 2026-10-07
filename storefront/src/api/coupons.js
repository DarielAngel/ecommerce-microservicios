// Cupones (servicio Promotions). Solo consulta: el uso real se aparta en el checkout de Órdenes,
// que vuelve a verificar todo con el subtotal real.
export const couponsApi = {
  validate: (client, code, subtotal) =>
    client.get('/api/coupons/validate', { params: { code: code.trim(), subtotal: subtotal.toFixed(2) } })
}
