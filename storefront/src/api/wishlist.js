// Favoritos del cliente (servicio Wishlist). Todas las llamadas requieren sesión: el servidor toma
// el usuario del token, así que nunca mandamos un id de usuario.
export const wishlistApi = {
  list: (client) => client.get('/api/wishlist'),
  add: (client, productId) => client.put(`/api/wishlist/${productId}`),
  remove: (client, productId) => client.delete(`/api/wishlist/${productId}`)
}
