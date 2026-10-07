// Puntos de lealtad del cliente (servicio Loyalty, Fase 6). El servidor toma al cliente del token.
export const loyaltyApi = {
  me: (client) => client.get('/api/loyalty/me'),
  quote: (client, amount) => client.get('/api/loyalty/me/quote', { params: { amount: amount.toFixed(2) } })
}
