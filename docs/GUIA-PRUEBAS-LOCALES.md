# Guía para probar la aplicación en local

Recorrido paso a paso para levantar todo en tu máquina, cargar datos realistas y probar cada
funcionalidad como cliente y como Admin. Los comandos son para **Windows (cmd)** desde la raíz
del repo; en WSL/Linux cambia `\` por `/` y `copy` por `cp`.

---

## 0. Requisitos (una sola vez)

| Herramienta | Para qué | Cómo comprobarlo |
|---|---|---|
| **Docker Desktop** (con al menos 8 GB de RAM asignados) | Levantar los 24 contenedores | `docker --version` |
| **Node.js 20+** y **pnpm** | Datos de demostración, pruebas de frontend y E2E | `node --version` · `corepack enable` y luego `pnpm --version` |
| **.NET 8 SDK** | Solo para correr las pruebas del backend | `dotnet --version` |

---

## 1. Configurar el `.env` (una sola vez)

```cmd
copy .env.example .env
```

Abre `.env` y cambia esta línea para poder pagar sin una cuenta real de PayPal:

```
PAYPAL_PROVIDER=Fake
```

Lo demás puede quedar como está. `RESEND_API_KEY` es opcional: sin ella todo funciona, solo que
los correos fallan con un error claro en los logs de Notificaciones.

---

## 2. Levantar todo

```cmd
docker compose up -d --build
docker compose ps
```

- La **primera vez** tarda entre 5 y 15 minutos (descarga imágenes y compila 10 servicios .NET y 2 frontends).
- `docker compose ps` debe mostrar **24 contenedores** en estado `running` (los Postgres y RabbitMQ en `healthy`).
- Si alguno está en `restarting` o `exited`, mira sus logs: `docker compose logs --tail=50 nombre-del-servicio`.

**Comprobación rápida** (debe responder sin error):

| Qué | URL |
|---|---|
| Gateway | http://localhost:5000/health |
| Catálogo vía Gateway | http://localhost:5000/api/categories |
| Tienda | http://localhost:5173 |
| Panel de Admin | http://localhost:8081 |
| RabbitMQ | http://localhost:15672 (usuario `guest`, clave `guest`) |

---

## 3. Cargar datos de demostración

```cmd
node scripts\seed-demo-data.mjs
```

Carga **23 categorías, 105 productos reales con imagen y stock, 10 cupones, 10 clientes y
344 reseñas**. Tarda 1–3 minutos. Al terminar imprime los accesos:

| Rol | Usuario | Contraseña |
|---|---|---|
| Admin (panel) | `admin@demo-tienda.test` | `Admin12345!` |
| Cliente (tienda) | `ana.martinez@demo-tienda.test` (hay 10, ej. `carlos.rodriguez@…`) | `Demo12345!` |
| Clave de acceso al panel | — | `clave-panel-de-desarrollo` (la de `ADMIN_PANEL_SITE_KEY` en `.env`) |

Se puede correr de nuevo cuando quieras: no duplica nada.

> **¿Ves "Categoría E2E …" o "Producto E2E …" en la tienda?** Son restos de haber corrido las
> pruebas end-to-end contra tu Docker. Bórralos (no toca tus usuarios, pedidos ni los datos de
> demostración) y recarga la página:
> ```cmd
> node scripts\limpiar-datos-e2e.mjs
> ```
> Con `--simular` solo muestra qué borraría.

---

## 4. Recorrido como cliente (tienda — http://localhost:5173)

Marca cada punto a medida que lo pruebas.

### 4.1 Portada, búsqueda y descubrimiento (Fases 0 y 4)
- [ ] La portada muestra el banner, las categorías como chips y productos con imagen, precio y estrellas.
- [ ] El botón de luna/sol cambia a **modo oscuro**; al recargar se mantiene.
- [ ] En el buscador escribe `audifonos` (sin tilde): aparecen **sugerencias** con imagen y precio (ej. *Sony Audífonos WH-1000XM5*).
- [ ] Con **↓ ↑** se resalta una sugerencia y **Enter** abre ese producto; **Esc** cierra el menú.
- [ ] Escribe `galaxy` y pulsa Enter sin elegir nada: lista completa de resultados.
- [ ] Filtra por categoría, precio y orden ("Precio: menor a mayor").

### 4.2 Detalle de producto (Fases 1 y 4)
- [ ] Abre un producto: imágenes, variantes (talla/color/capacidad), precio y reseñas con su distribución de estrellas.
- [ ] Algunos productos muestran la insignia **"¡Quedan solo N!"** (stock real ≤ 5) y otros **"Agotado"** con el botón *Sin stock*. Para forzarlo: en el panel de Admin → Inventario, deja una variante en 3 unidades y recarga el producto.
- [ ] Abajo aparece **"También te puede interesar"** con productos de la misma categoría; al hacer clic en uno, la página cambia a ese producto.
- [ ] Visita 3 o 4 productos y vuelve a la portada: aparece **"Vistos recientemente"**. *Borrar historial* lo vacía.

### 4.3 Sesión, favoritos y reseñas (Fases 1 y 2)
- [ ] Toca el corazón sin sesión: te lleva a iniciar sesión y te devuelve al producto.
- [ ] Inicia sesión con `ana.martinez@demo-tienda.test` / `Demo12345!`.
- [ ] Marca 2 o 3 **favoritos** (corazón en tarjetas o en el detalle): el contador del encabezado sube.
- [ ] En **Mis favoritos** usa *Mover al carrito* en uno con una sola variante: desaparece de la lista y sube el contador del carrito.
- [ ] En un producto que Ana no haya reseñado, **publica una reseña**; edítala y luego elimínala.

### 4.4 Carrito, cupones y pago (Fase 3)
- [ ] En el **carrito** cambia cantidades; si pides más del stock disponible, aparece el error y la cantidad vuelve atrás.
- [ ] *Continuar al checkout* → en **¿Tienes un cupón?** prueba:

  | Cupón | Qué deberías ver |
  |---|---|
  | `bienvenida10` | 10 % de descuento (máx. $30). Solo una vez por cliente. |
  | `ahorra5` | $5 menos si la compra es de $40 o más; si no, el mensaje dice cuánto falta. |
  | `tech50` | $50 menos en compras desde $500 (prueba con una laptop o un celular). |
  | `verano15` | "Este cupón ya venció." |
  | `blackfriday` | "Todavía no está vigente" (empieza el próximo Black Friday). |
  | `noexiste` | "El cupón "NOEXISTE" no existe." |

- [ ] Con un cupón aplicado: escribe una dirección y *Pagar con PayPal*. Con `PAYPAL_PROVIDER=Fake` no se abre PayPal; en la pantalla de espera pulsa **"Ya aprobé el pago — confirmar"**.
- [ ] Verás **¡Pago confirmado!**, el total pagado y *"Ahorraste $X con el cupón …"*.
- [ ] En **Mis pedidos** la orden aparece *Pagada* con el cupón usado.
- [ ] Repite el checkout con `bienvenida10` en otra compra de la misma cuenta: ahora dice que **ya lo usaste**.

---

## 5. Recorrido como Admin (panel — http://localhost:8081)

- [ ] Ingresa la **clave de acceso** (`clave-panel-de-desarrollo`) y luego `admin@demo-tienda.test` / `Admin12345!`.
- [ ] **Productos**: busca, edita uno y desactívalo; en la tienda deja de aparecer (y en las sugerencias).
- [ ] **Categorías**: crea una subcategoría.
- [ ] **Inventario**: busca un producto, elige la variante y ajusta su stock (ej. a 3) → en la tienda aparece "¡Quedan solo 3!".
- [ ] **Cupones**: verás los 10 con su estado (*Activo, Programado, Vencido, Pausado*). Crea uno, edítalo, páusalo y comprueba en la tienda que deja de funcionar. La columna *Usos* sube con cada compra pagada.
- [ ] **Órdenes**: la orden que pagaste aparece con el cupón y el descuento; márcala como **enviada**.
- [ ] **Notificaciones**: registro de los correos de "pago confirmado" y "pedido enviado" (con `RESEND_API_KEY` llegan de verdad a tu correo).

---

## 6. Ver qué pasa por dentro

| Qué | Cómo |
|---|---|
| Logs de un servicio en vivo | `docker compose logs -f orders-service` (o `promotions-service`, `catalog-service`…) |
| Mensajes entre servicios | http://localhost:15672 → *Queues*: verás las colas de `order-paid`, `variant-created`, etc. |
| Probar una API a mano (Swagger) | Cada servicio en su puerto: http://localhost:5002/swagger (Catálogo), 5003 (Inventario), 5006 (Órdenes), 5010 (Cupones)… |
| Consultar una base de datos | `docker exec -it ecommerce-postgres-orders psql -U orders_svc -d orders_db` y luego `SELECT id, status, coupon_code, discount_amount FROM orders;` (`\q` para salir) |

Puertos de cada servicio: Users 5001 · Catálogo 5002 · Inventario 5003 · Carrito 5004 · Pagos 5005 ·
Órdenes 5006 · Notificaciones 5007 · Reseñas 5008 · Favoritos 5009 · Cupones 5010.

---

## 7. Pruebas automáticas

**Frontends** (no necesitan nada levantado):
```cmd
cd storefront && pnpm install && pnpm run test:unit && cd ..
cd admin-panel && pnpm install && pnpm run test:unit && cd ..
```

**Backend** (necesitan Docker Desktop abierto: cada proyecto levanta su propio Postgres/RabbitMQ):
```cmd
for /r src %f in (*Tests.csproj) do dotnet test "%f"
```
(Para uno solo: `dotnet test src\Services\Promotions\Promotions.Tests\Promotions.IntegrationTests`.)

**End-to-end** (necesitan todo levantado con `PAYPAL_PROVIDER=Fake`):
```cmd
cd e2e
pnpm install
pnpm exec playwright install chromium
pnpm test
```
`pnpm run test:ui` abre la interfaz visual de Playwright para ver cada paso.

---

## 8. Problemas comunes

| Síntoma | Causa probable y solución |
|---|---|
| Un contenedor queda en `restarting` | `docker compose logs --tail=80 <servicio>` muestra el error. Suele ser un puerto ocupado o un Postgres que aún no arrancó: `docker compose up -d` otra vez. |
| Cambié código y no veo el cambio | Hay que reconstruir ese servicio: `docker compose up -d --build <servicio>` (o `--build` sin nombre para todos). |
| El checkout falla con **502** | `PAYPAL_PROVIDER` no está en `Fake` (o faltan credenciales reales). Cámbialo en `.env` y `docker compose up -d --build payments-service`. |
| El script de datos dice *No se pudo crear el Admin* | La `ADMIN_PROVISIONING_KEY` del `.env` no coincide con la del contenedor `users-service`: reconstrúyelo con `docker compose up -d --build users-service`. |
| "Esperando a el servicio de cupones…" sin fin | `promotions-service` no arrancó: `docker compose logs promotions-service`. |
| Quiero empezar con la base vacía | `docker compose down -v` (**borra todos los datos**) y luego `docker compose up -d` y el script de datos otra vez. |
| La tienda muestra solo productos "E2E" | No se cargaron los datos de demostración en esta base: `node scripts\seed-demo-data.mjs`. Los restos de las pruebas se borran con `node scripts\limpiar-datos-e2e.mjs`. |
| La búsqueda sin tildes no encuentra nada | Catálogo instala la extensión `unaccent` al arrancar; si se cayó antes, reinícialo: `docker compose restart catalog-service`. |
| `no such host` al construir imágenes | Problema de DNS de Docker/WSL: ver *Troubleshooting general* en el README. |
| `Resource temporarily unavailable (api.nuget.org)` o `short read / unexpected EOF` al construir | Se cortó la red o el DNS de Docker a mitad de la descarga. Vuelve a correr el mismo comando: lo ya descargado (imágenes y paquetes NuGet/pnpm) queda en caché y solo se baja lo que faltaba. Si la red está muy inestable, construye de a un servicio (ver abajo). |
| Reconstruyó **todos** los servicios aunque cambié poco | Microsoft publicó una versión nueva de la imagen `dotnet/sdk:8.0`. Ya no hace falta volver a bajar los paquetes: quedan en la caché de BuildKit, compartida por todos los servicios. **No** corras `docker builder prune` (borraría esa caché). |

**Construir de a un servicio** (útil con internet inestable; cada uno que termina queda guardado):
```cmd
for %s in (gateway users-service catalog-service inventory-service cart-service payments-service orders-service notifications-service reviews-service wishlist-service promotions-service storefront admin-panel) do docker compose build %s
docker compose up -d
```
