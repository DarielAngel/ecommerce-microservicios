// Identidad de la tienda. Se fija al compilar (variables VITE_*), así cada despliegue puede
// llamarse distinto sin tocar el código.
export const STORE = {
  name: import.meta.env.VITE_STORE_NAME || 'Tienda',
  tagline: import.meta.env.VITE_STORE_TAGLINE || 'Todo lo que necesitas, en un solo lugar'
}
