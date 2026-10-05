/// Divide las categorías en las que se muestran como chips y las que van en el selector
/// "Más categorías", para que una tienda con muchas categorías no empuje los productos fuera de la pantalla.
/// La categoría seleccionada siempre queda visible (ocupa el último chip) y no se pierde ninguna.
export function splitCategories(categories, selectedId, max = 7) {
  if (categories.length <= max) return { visible: categories, overflow: [] }

  let visible = categories.slice(0, max)
  let overflow = categories.slice(max)

  const selected = overflow.find((c) => c.id === selectedId)
  if (selected) {
    const displaced = visible[max - 1]
    visible = [...visible.slice(0, max - 1), selected]
    overflow = [...overflow.filter((c) => c.id !== selectedId), displaced]
  }

  return { visible, overflow }
}
