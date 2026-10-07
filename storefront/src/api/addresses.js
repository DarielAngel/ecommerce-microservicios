// Libreta de direcciones del cliente (servicio Users, Fase 5). El servidor toma al cliente del
// token: nunca mandamos un id de usuario.
export const addressesApi = {
  list: (client) => client.get('/api/addresses'),
  create: (client, input) => client.post('/api/addresses', input),
  update: (client, id, input) => client.put(`/api/addresses/${id}`, input),
  setDefault: (client, id) => client.post(`/api/addresses/${id}/default`),
  remove: (client, id) => client.delete(`/api/addresses/${id}`)
}
