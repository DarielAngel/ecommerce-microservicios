# E-commerce — Paso 1: Infraestructura base

Este primer módulo NO tiene lógica de negocio todavía. Su único objetivo es
dejar levantada la infraestructura (bases de datos, message broker y el
Gateway) para que sobre ella construyamos, uno por uno, los microservicios
reales: Users/Auth → Catálogo → Inventario → Carrito → Órdenes → Pagos →
Notificaciones → Frontend.

## Requisitos previos en tu máquina

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (con Docker Compose v2)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — solo lo necesitarás para los próximos módulos, no para este paso.

## Cómo crear la solución (una sola vez)

Desde la raíz del proyecto (`ecommerce/`):

```bash
dotnet new sln -n Ecommerce
dotnet sln add src/Gateway/Gateway.csproj
```

Esto genera `Ecommerce.sln`, que iremos actualizando con `dotnet sln add`
cada vez que agreguemos un microservicio nuevo.

## Cómo levantar todo

```bash
docker compose up -d --build
```

Esto levanta:
- 6 instancias de PostgreSQL (una por microservicio futuro: users, catalog, cart, inventory, orders, payments)
- RabbitMQ (con panel de administración)
- El Gateway (.NET + YARP), todavía sin rutas configuradas

## Cómo verificar que el Paso 1 quedó bien (checklist)

1. **Todos los contenedores están arriba:**
   ```bash
   docker compose ps
   ```
   Todos deben aparecer como `running` o `healthy`.

2. **El Gateway responde:**
   ```bash
   curl http://localhost:5000/health
   ```
   Debe devolver algo como:
   ```json
   {"status":"ok","service":"gateway","timestampUtc":"..."}
   ```

3. **RabbitMQ está accesible:** abre [http://localhost:15672](http://localhost:15672) en el navegador (usuario: `guest`, contraseña: `guest`). Debe cargar el panel de administración.

4. **Cada Postgres acepta conexiones** (opcional pero recomendado), por ejemplo para el de Users:
   ```bash
   docker exec -it ecommerce-postgres-users psql -U users_svc -d users_db -c "SELECT 1;"
   ```
   Debe devolver `1` sin errores. Repite el mismo patrón cambiando el nombre del contenedor (`ecommerce-postgres-catalog`, `ecommerce-postgres-cart`, etc.) si quieres confirmar todas.

## Puertos usados

| Servicio | Puerto host |
|---|---|
| Gateway | 5000 |
| RabbitMQ (AMQP) | 5672 |
| RabbitMQ (UI) | 15672 |
| Postgres Users | 5433 |
| Postgres Catalog | 5434 |
| Postgres Cart | 5435 |
| Postgres Inventory | 5436 |
| Postgres Orders | 5437 |
| Postgres Payments | 5438 |

## Siguiente paso

Cuando confirmes que los 4 puntos del checklist pasan sin errores, seguimos
con el **Módulo 2: Users/Auth** (registro, login, JWT, roles), que se
conectará a `postgres-users` y se registrará como la primera ruta real en
el Gateway.

Si algo falla, copia aquí el error exacto de `docker compose ps` o de los
logs (`docker compose logs gateway` / `docker compose logs postgres-users`)
y lo resolvemos antes de seguir.

---

# Módulo 2: Users / Auth

Microservicio completo con Clean Architecture (Domain, Application,
Infrastructure, Api) que implementa registro, login, refresh de tokens JWT
(con rotación) y un endpoint especial protegido para crear administradores.

## 1. Agregar los proyectos a la solución

Desde la raíz del proyecto (`ecommerce/`):

```bash
dotnet sln add src/Services/Users/Users.Domain/Users.Domain.csproj
dotnet sln add src/Services/Users/Users.Application/Users.Application.csproj
dotnet sln add src/Services/Users/Users.Infrastructure/Users.Infrastructure.csproj
dotnet sln add src/Services/Users/Users.Api/Users.Api.csproj
dotnet sln add src/Services/Users/Users.Tests/Users.UnitTests/Users.UnitTests.csproj
dotnet sln add src/Services/Users/Users.Tests/Users.IntegrationTests/Users.IntegrationTests.csproj
```

## 2. Correr las pruebas ANTES de levantar los contenedores

Esto es justo la verificación por capas que pediste: primero unitarias
(rápidas, sin nada externo), luego integración (con Postgres real).

```bash
# Pruebas unitarias (Domain + Application, sin base de datos, deben ser instantáneas)
dotnet test src/Services/Users/Users.Tests/Users.UnitTests

# Pruebas de integración (requieren Docker corriendo: levantan un Postgres real con Testcontainers)
dotnet test src/Services/Users/Users.Tests/Users.IntegrationTests
```

Todas deben pasar en verde antes de continuar.

## 3. Levantar el servicio completo (con el resto de la infraestructura)

```bash
docker compose up -d --build
```

Esto ahora también construye y levanta `users-service`, conectado a
`postgres-users`, y el Gateway ya enruta `/api/auth/**` y `/api/admins/**`
hacia él.

## 4. Checklist de verificación manual

> **Nota sobre los comandos:** abajo va primero la versión **bash** (macOS/Linux/Git Bash/WSL) y luego la versión **cmd.exe de Windows** (comillas dobles escapadas, sin `\` de continuación de línea, porque en `cmd.exe` las comillas simples `'...'` y el `\` al final de línea NO funcionan como en bash). Si usas PowerShell, o bien usa la versión de `cmd.exe` (curl.exe funciona igual), o usa `Invoke-RestMethod` (ver nota al final).

1. **Healthcheck directo del servicio** (sin pasar por el Gateway) — igual en ambos:
   ```bash
   curl http://localhost:5001/health
   ```

2. **Registro de un cliente A TRAVÉS DEL GATEWAY** (esto confirma en un solo paso que el enrutamiento YARP `/api/auth/**` → `users-service` funciona):

   **bash:**
   ```bash
   curl -X POST http://localhost:5000/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{"email":"cliente@test.com","password":"Password123","fullName":"Cliente de Prueba"}'
   ```

   **cmd.exe (Windows):**
   ```cmd
   curl -X POST http://localhost:5000/api/auth/register -H "Content-Type: application/json" -d "{\"email\":\"cliente@test.com\",\"password\":\"Password123\",\"fullName\":\"Cliente de Prueba\"}"
   ```

   Debe devolver `201 Created` con `accessToken` y `refreshToken`.

3. **Login:**

   **bash:**
   ```bash
   curl -X POST http://localhost:5000/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"cliente@test.com","password":"Password123"}'
   ```

   **cmd.exe (Windows):**
   ```cmd
   curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"cliente@test.com\",\"password\":\"Password123\"}"
   ```

   Debe devolver `200 OK` con tokens nuevos.

4. **Perfil autenticado** (reemplaza `TOKEN` por el `accessToken` completo que devolvió el login — **sin** los símbolos `<` `>`, esos son solo un placeholder visual y en `cmd.exe` `<` significa "leer desde un archivo"):

   **bash:**
   ```bash
   curl http://localhost:5000/api/auth/me \
     -H "Authorization: Bearer TOKEN"
   ```

   **cmd.exe (Windows)** — más cómodo si guardas el token en una variable primero:
   ```cmd
   set TOKEN=pega_aqui_el_accessToken_completo
   curl http://localhost:5000/api/auth/me -H "Authorization: Bearer %TOKEN%"
   ```

   Debe devolver `200 OK` con el email, nombre y rol `Cliente`.

5. **Crear un Admin** (usa la API key de desarrollo definida en `docker-compose.yml`, `clave-admin-de-desarrollo`, o la que hayas puesto en la variable de entorno `ADMIN_PROVISIONING_KEY`):

   **bash:**
   ```bash
   curl -X POST http://localhost:5000/api/admins \
     -H "Content-Type: application/json" \
     -H "X-Admin-Provisioning-Key: clave-admin-de-desarrollo" \
     -d '{"email":"admin@test.com","password":"Password123","fullName":"Admin Principal"}'
   ```

   **cmd.exe (Windows):**
   ```cmd
   curl -X POST http://localhost:5000/api/admins -H "Content-Type: application/json" -H "X-Admin-Provisioning-Key: clave-admin-de-desarrollo" -d "{\"email\":\"admin@test.com\",\"password\":\"Password123\",\"fullName\":\"Admin Principal\"}"
   ```

   Debe devolver `201 Created`. Si cambias la cabecera por una clave incorrecta, debe devolver `401`.

> **PowerShell**, si prefieres usarlo en vez de `cmd.exe`, puede usar `Invoke-RestMethod` (nota: `Invoke-RestMethod` NO existe en `cmd.exe`, solo en PowerShell de verdad — ábrelo desde el menú de inicio buscando "PowerShell", no "Símbolo del sistema"):
> ```powershell
> Invoke-RestMethod -Uri "http://localhost:5000/api/auth/register" -Method POST -ContentType "application/json" -Body '{"email":"cliente2@test.com","password":"Password123","fullName":"Cliente de Prueba"}'
> ```

## 5. Variables de entorno importantes (no usar los valores de ejemplo en producción)

| Variable | Uso |
|---|---|
| `JWT_SECRET` | Firma los access tokens. Cambiar por un valor largo y aleatorio real. |
| `ADMIN_PROVISIONING_KEY` | Protege la creación de administradores. Cambiar y mantener en secreto. |

Puedes definirlas antes de levantar los contenedores. Estas variables solo
valen para la sesión de terminal actual — si cierras la ventana hay que
volver a definirlas antes del próximo `docker compose up`.

**bash (macOS/Linux/Git Bash/WSL):**
```bash
export JWT_SECRET="$(openssl rand -base64 48)"
export ADMIN_PROVISIONING_KEY="$(openssl rand -base64 32)"
docker compose up -d --build
```

**cmd.exe (Windows)** — `export` no existe, se usa `set`, y `openssl` normalmente no está instalado en Windows, así que puedes usar cualquier texto largo y aleatorio a mano:
```cmd
set JWT_SECRET=un-valor-largo-y-aleatorio-cambia-esto-1234567890
set ADMIN_PROVISIONING_KEY=otra-clave-secreta-distinta-cambia-esto
docker compose up -d --build
```

**PowerShell** (si quieres algo generado, ya que no hay `openssl`):
```powershell
$env:JWT_SECRET = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
$env:ADMIN_PROVISIONING_KEY = [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
docker compose up -d --build
```

## Siguiente paso

Cuando los 6 puntos del checklist pasen y los tests unitarios + de
integración estén en verde, seguimos con el **Módulo 3: Catálogo de
productos** (CRUD de productos, categorías, búsqueda y paginación),
conectado a `postgres-catalog` y con su propia ruta en el Gateway.

Si algo falla en este módulo, copia el error exacto (log del contenedor,
salida de `dotnet test`, o respuesta HTTP inesperada) y lo resolvemos
antes de avanzar.

### Troubleshooting conocido: `System.ArgumentException: Host can't be null`

Si al correr los tests de integración ves este error dentro de
`InitializeAsync()`, la causa es que `appsettings.json` trae la connection
string como **string vacío** (`""`), no `null`. Con el modelo de hosting
mínimo de .NET 8, sobreescribir la configuración vía
`ConfigureAppConfiguration`/`AddInMemoryCollection` en el `WebApplicationFactory`
no tiene prioridad garantizada sobre `appsettings.json`, así que el string
vacío "gana" y Npgsql termina sin ningún `Host=`. Ya está corregido en este
proyecto: los factories de test (`UsersApiFactory`, `CatalogApiFactory`)
usan **variables de entorno** (`Environment.SetEnvironmentVariable`) en vez
de `ConfigureAppConfiguration`, porque las variables de entorno sí tienen
prioridad garantizada. Si creas un factory de test nuevo para un módulo
futuro, sigue ese mismo patrón.

---

# Módulo 3: Catálogo de productos

Microservicio con Clean Architecture completa. Productos con **variantes**
(cada una con su propio SKU/precio/atributos como talla o color), **subida
real de imágenes** (guardadas en un volumen de Docker y servidas como
archivos estáticos), y **búsqueda pública** por coincidencia parcial en
nombre/descripción + filtros de categoría y precio, con paginación.

Este servicio **no emite** JWT (eso lo hace Users), pero sí los **valida**:
comparte el mismo `JWT_SECRET` que `users-service`, así que un token emitido
al hacer login en Users sirve directamente aquí para las operaciones de
Admin (crear categorías, productos, subir imágenes).

## 1. Agregar los proyectos a la solución

```bash
dotnet sln add src/Services/Catalog/Catalog.Domain/Catalog.Domain.csproj
dotnet sln add src/Services/Catalog/Catalog.Application/Catalog.Application.csproj
dotnet sln add src/Services/Catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj
dotnet sln add src/Services/Catalog/Catalog.Api/Catalog.Api.csproj
dotnet sln add src/Services/Catalog/Catalog.Tests/Catalog.UnitTests/Catalog.UnitTests.csproj
dotnet sln add src/Services/Catalog/Catalog.Tests/Catalog.IntegrationTests/Catalog.IntegrationTests.csproj
```

## 2. Correr las pruebas ANTES de levantar los contenedores

```bash
dotnet test src/Services/Catalog/Catalog.Tests/Catalog.UnitTests
dotnet test src/Services/Catalog/Catalog.Tests/Catalog.IntegrationTests
```

Las de integración incluyen: creación de categorías, protección por rol
(`401` sin token, `403` con rol `Cliente`, éxito con rol `Admin`), creación
de producto con variantes, y **búsqueda por una parte del nombre** —
exactamente el comportamiento que pediste.

## 3. Levantar todo (asegúrate de usar el MISMO `JWT_SECRET` que en el Módulo 2)

**bash:**
```bash
export JWT_SECRET="$(openssl rand -base64 48)"   # si no lo exportaste ya en el Módulo 2
export ADMIN_PROVISIONING_KEY="clave-admin-de-desarrollo"
docker compose up -d --build
```

**cmd.exe (Windows):**
```cmd
set JWT_SECRET=el-mismo-valor-que-usaste-en-el-modulo-2
set ADMIN_PROVISIONING_KEY=clave-admin-de-desarrollo
docker compose up -d --build
```

⚠️ Importante: si vuelves a levantar los contenedores en otra sesión de
terminal, define la **misma** `JWT_SECRET` que usaste antes. Si cambia,
los tokens emitidos por Users dejan de ser válidos en Catálogo (y
viceversa). Si no defines nada, `docker-compose.yml` ya trae un valor por
defecto que funciona igual en ambos servicios — no es obligatorio definir
la variable para que el checklist funcione.

## 4. Checklist de verificación manual

> Igual que en el Módulo 2: primero la versión **bash**, luego la versión
> **cmd.exe de Windows**. Los `<PLACEHOLDERS>` van sin los símbolos `<` `>`
> — en `cmd.exe` especialmente, `<` significa "leer desde un archivo" y
> rompe el comando.

1. **Healthcheck directo** — igual en ambos:
   ```bash
   curl http://localhost:5002/health
   ```

2. **Crear un Admin y loguearte** (si todavía no tienes uno del Módulo 2):

   **bash:**
   ```bash
   curl -X POST http://localhost:5000/api/admins \
     -H "Content-Type: application/json" \
     -H "X-Admin-Provisioning-Key: clave-admin-de-desarrollo" \
     -d '{"email":"admin@test.com","password":"Password123","fullName":"Admin"}'

   curl -X POST http://localhost:5000/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"admin@test.com","password":"Password123"}'
   ```

   **cmd.exe (Windows):**
   ```cmd
   curl -X POST http://localhost:5000/api/admins -H "Content-Type: application/json" -H "X-Admin-Provisioning-Key: clave-admin-de-desarrollo" -d "{\"email\":\"admin@test.com\",\"password\":\"Password123\",\"fullName\":\"Admin\"}"

   curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"admin@test.com\",\"password\":\"Password123\"}"
   ```

   Copia el `accessToken` de la respuesta. En `cmd.exe` conviene guardarlo en una variable para no repegarlo en cada comando:
   ```cmd
   set TOKEN=pega_aqui_el_accessToken_completo
   ```

3. **Crear una categoría:**

   **bash** (reemplaza `TOKEN`):
   ```bash
   curl -X POST http://localhost:5000/api/categories \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer TOKEN" \
     -d '{"name":"Ropa","parentCategoryId":null}'
   ```

   **cmd.exe (Windows)** (usa la variable `%TOKEN%` ya guardada):
   ```cmd
   curl -X POST http://localhost:5000/api/categories -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"name\":\"Ropa\",\"parentCategoryId\":null}"
   ```

   Guarda el `id` que devuelve:
   ```cmd
   set CATEGORY_ID=pega_aqui_el_id
   ```

4. **Crear un producto con variantes:**

   **bash** (reemplaza `CATEGORY_ID` y `TOKEN`):
   ```bash
   curl -X POST http://localhost:5000/api/products \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer TOKEN" \
     -d '{
       "name": "Camiseta Edición Especial",
       "description": "Camiseta 100% algodón, edición limitada",
       "categoryId": "CATEGORY_ID",
       "variants": [
         { "sku": "CAM-M-NEGRO", "price": 25.50, "attributes": { "Talla": "M", "Color": "Negro" } },
         { "sku": "CAM-L-NEGRO", "price": 27.00, "attributes": { "Talla": "L", "Color": "Negro" } }
       ]
     }'
   ```

   **cmd.exe (Windows)** — nota el `%%` en vez de `%` para el símbolo de porcentaje ("100% algodón"), porque en `cmd.exe` `%` es un carácter especial y hay que escribirlo doble:
   ```cmd
   curl -X POST http://localhost:5000/api/products -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"name\":\"Camiseta Edicion Especial\",\"description\":\"Camiseta 100%% algodon, edicion limitada\",\"categoryId\":\"%CATEGORY_ID%\",\"variants\":[{\"sku\":\"CAM-M-NEGRO\",\"price\":25.50,\"attributes\":{\"Talla\":\"M\",\"Color\":\"Negro\"}},{\"sku\":\"CAM-L-NEGRO\",\"price\":27.00,\"attributes\":{\"Talla\":\"L\",\"Color\":\"Negro\"}}]}"
   ```

   Guarda el `id` del producto devuelto:
   ```cmd
   set PRODUCT_ID=pega_aqui_el_id
   ```

5. **Subir una imagen:**

   **bash** (reemplaza `PRODUCT_ID`, `TOKEN` y la ruta a un `.jpg`/`.png` real):
   ```bash
   curl -X POST http://localhost:5000/api/products/PRODUCT_ID/images \
     -H "Authorization: Bearer TOKEN" \
     -F "file=@/ruta/a/una/imagen.jpg" \
     -F "isPrimary=true"
   ```

   **cmd.exe (Windows)** (usa una ruta de Windows con `\`, y si el nombre del archivo tiene espacios, va entre comillas dentro del valor de `-F`):
   ```cmd
   curl -X POST http://localhost:5000/api/products/%PRODUCT_ID%/images -H "Authorization: Bearer %TOKEN%" -F "file=@C:\ruta\a\una\imagen.jpg" -F "isPrimary=true"
   ```

   Debe devolver `200 OK` con el `fileName` guardado. Verifica que la imagen
   se vea en el navegador en `http://localhost:5000/images/<fileName>`.

6. **Búsqueda pública, SIN token, por una parte del nombre** — igual en ambos:
   ```bash
   curl "http://localhost:5000/api/products?searchTerm=Edicion"
   ```
   Debe encontrar el producto aunque escribas solo una parte del nombre
   (prueba también buscando por una palabra de la descripción).

7. **Filtro por rango de precio** — igual en ambos:
   ```bash
   curl "http://localhost:5000/api/products?minPrice=20&maxPrice=30"
   ```

8. **Seguridad por rol** (con el token de un Cliente registrado normal, no Admin):

   **bash** (reemplaza `TOKEN_DE_CLIENTE`):
   ```bash
   curl -X POST http://localhost:5000/api/categories \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer TOKEN_DE_CLIENTE" \
     -d '{"name":"No debería poder","parentCategoryId":null}'
   ```

   **cmd.exe (Windows):**
   ```cmd
   set TOKEN_CLIENTE=pega_aqui_el_token_de_un_cliente_normal
   curl -X POST http://localhost:5000/api/categories -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN_CLIENTE%" -d "{\"name\":\"No deberia poder\",\"parentCategoryId\":null}"
   ```

   Debe devolver `403 Forbidden`.

---

# Módulo 4: Inventario

Microservicio de stock por variante, con reservas atómicas (todo o nada)
para el futuro flujo de checkout. Se entera de que existe una variante
nueva **automáticamente**, escuchando un evento de RabbitMQ que publica
Catálogo — no hace falta registrarla a mano.

## 1. Agregar los proyectos a la solución

```bash
dotnet sln add src/Shared/Contracts/Contracts.csproj
dotnet sln add src/Services/Inventory/Inventory.Domain/Inventory.Domain.csproj
dotnet sln add src/Services/Inventory/Inventory.Application/Inventory.Application.csproj
dotnet sln add src/Services/Inventory/Inventory.Infrastructure/Inventory.Infrastructure.csproj
dotnet sln add src/Services/Inventory/Inventory.Api/Inventory.Api.csproj
dotnet sln add src/Services/Inventory/Inventory.Tests/Inventory.UnitTests/Inventory.UnitTests.csproj
dotnet sln add src/Services/Inventory/Inventory.Tests/Inventory.IntegrationTests/Inventory.IntegrationTests.csproj
```

## 2. Correr los tests (requieren Docker: levantan Postgres Y RabbitMQ reales)

```bash
dotnet test src/Services/Inventory/Inventory.Tests/Inventory.UnitTests
dotnet test src/Services/Inventory/Inventory.Tests/Inventory.IntegrationTests
```

Los de integración incluyen un test que publica el evento `VariantCreated`
directamente al bus y espera (con timeout) a que Inventario lo procese y
cree el stock automáticamente — así queda probado el "idioma común" entre
microservicios de punta a punta, no solo simulado.

## 3. Levantar todo

```bash
docker compose up -d --build
```

## 4. Checklist de verificación manual

1. **Healthcheck directo:**
   ```bash
   curl http://localhost:5003/health
   ```

2. **Crear un producto en Catálogo (dispara el evento)** — usa los mismos
   comandos del Módulo 3 para loguearte como Admin y crear un producto con
   variantes. Copia el `id` de una variante de la respuesta (`variants[0].id`).

3. **Esperar unos segundos** (el consumo del evento es asíncrono) y consultar el stock:

   **bash:**
   ```bash
   curl http://localhost:5000/api/stock/VARIANT_ID -H "Authorization: Bearer TOKEN"
   ```

   **cmd.exe (Windows):**
   ```cmd
   curl http://localhost:5000/api/stock/%VARIANT_ID% -H "Authorization: Bearer %TOKEN%"
   ```

   Debe devolver `200 OK` con `quantityOnHand: 0` — confirma que el evento viajó de Catálogo a Inventario solo.

4. **Ajustar el stock (recepción de mercadería), como Admin:**

   **cmd.exe (Windows):**
   ```cmd
   curl -X POST http://localhost:5000/api/stock/%VARIANT_ID%/adjust -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"newQuantityOnHand\":50}"
   ```

5. **Reservar stock** (simulando el checkout de una orden):
   ```cmd
   set ORDER_ID=11111111-1111-1111-1111-111111111111
   curl -X POST http://localhost:5000/api/stock/reservations -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"orderId\":\"%ORDER_ID%\",\"items\":[{\"variantId\":\"%VARIANT_ID%\",\"quantity\":5}]}"
   ```
   Consulta el stock de nuevo: `quantityAvailable` debe bajar en 5, pero `quantityOnHand` sigue igual (todavía no se confirmó).

6. **Confirmar la reserva** (pago exitoso):
   ```cmd
   curl -X POST http://localhost:5000/api/stock/reservations/%ORDER_ID%/confirm -H "Authorization: Bearer %TOKEN%"
   ```
   Ahora sí `quantityOnHand` baja definitivamente.

7. **Probar sin stock suficiente** (reserva otra orden pidiendo una cantidad absurda, ej. 99999): debe devolver `409 Conflict`.

8. **Bajo stock** (Admin):
   ```cmd
   curl http://localhost:5000/api/stock/low-stock -H "Authorization: Bearer %TOKEN%"
   ```

---

# Módulo 5: Carrito de compras

Un carrito por usuario logueado (no hay carrito de invitado). Cada vez que
agregas o actualizas un ítem, Carrito llama **en tiempo real** a Catálogo
(para traer nombre/SKU/precio) y a Inventario (para validar que haya stock
disponible) — no son eventos asíncronos, son llamadas HTTP directas entre
microservicios. El precio de cada línea queda "congelado" desde el momento
en que se agrega: si el precio cambia después en Catálogo, tu carrito no se entera.

## 1. Agregar los proyectos a la solución

```bash
dotnet sln add src/Services/Cart/Cart.Domain/Cart.Domain.csproj
dotnet sln add src/Services/Cart/Cart.Application/Cart.Application.csproj
dotnet sln add src/Services/Cart/Cart.Infrastructure/Cart.Infrastructure.csproj
dotnet sln add src/Services/Cart/Cart.Api/Cart.Api.csproj
dotnet sln add src/Services/Cart/Cart.Tests/Cart.UnitTests/Cart.UnitTests.csproj
dotnet sln add src/Services/Cart/Cart.Tests/Cart.IntegrationTests/Cart.IntegrationTests.csproj
```

## 2. Correr los tests

```bash
dotnet test src/Services/Cart/Cart.Tests/Cart.UnitTests
dotnet test src/Services/Cart/Cart.Tests/Cart.IntegrationTests
```

Los de integración usan **Postgres real** (Testcontainers) pero **no**
levantan Catálogo ni Inventario reales — los reemplazan por "fakes" en
memoria (`FakeCatalogServiceClient`/`FakeInventoryServiceClient`), así
Carrito se prueba de forma aislada. La comunicación real entre los tres
servicios se verifica en el checklist manual de abajo, contra los
contenedores de verdad.

## 3. Levantar todo

```bash
docker compose up -d --build
```

## 4. Checklist de verificación manual

1. **Healthcheck directo:**
   ```cmd
   curl http://localhost:5004/health
   ```

2. **Sin token, debe fallar:**
   ```cmd
   curl -i http://localhost:5000/api/cart
   ```
   Debe devolver `401`.

3. **Carrito vacío al principio** (usa el token de un Cliente normal, no Admin):
   ```cmd
   curl http://localhost:5000/api/cart -H "Authorization: Bearer %TOKEN%"
   ```
   Debe devolver `200 OK` con `"items":[]`.

4. **Agregar un ítem** (usa un `VARIANT_ID` real de un producto que ya creaste en el Módulo 3, y que ya tenga stock ajustado en el Módulo 4 — si no, primero ajusta su stock):
   ```cmd
   curl -X POST http://localhost:5000/api/cart/items -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"variantId\":\"%VARIANT_ID%\",\"quantity\":2}"
   ```
   Debe devolver `200 OK` con el ítem agregado, `unitPrice` tomado de Catálogo, y el `subtotal` calculado.

5. **Agregar la misma variante de nuevo** (con otra cantidad): debe **sumarse** a la misma línea, no crear una línea nueva. Revisa que `unitPrice` siga siendo el mismo de la primera vez, aunque hayas cambiado el precio en Catálogo mientras tanto.

6. **Pedir más de lo que hay disponible:**
   ```cmd
   curl -X POST http://localhost:5000/api/cart/items -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"variantId\":\"%VARIANT_ID%\",\"quantity\":99999}"
   ```
   Debe devolver `409 Conflict`.

7. **Actualizar cantidad:**
   ```cmd
   curl -X PUT http://localhost:5000/api/cart/items/%VARIANT_ID% -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"quantity\":1}"
   ```

8. **Quitar el ítem:**
   ```cmd
   curl -X DELETE http://localhost:5000/api/cart/items/%VARIANT_ID% -H "Authorization: Bearer %TOKEN%"
   ```

9. **Vaciar el carrito completo:**
   ```cmd
   curl -X DELETE http://localhost:5000/api/cart -H "Authorization: Bearer %TOKEN%"
   ```

---

# Módulo 6: Pagos (PayPal)

A diferencia de todos los módulos anteriores, este habla con un **servicio
externo real** (PayPal, en modo sandbox/pruebas). Vas a necesitar una
cuenta gratuita de desarrollador y probar el flujo de aprobación abriendo
una URL en el navegador — no se puede hacer 100% por `curl`, porque el
comprador tiene que loguearse en PayPal para aprobar el pago.

## 1. Conseguir credenciales de sandbox de PayPal (una sola vez)

1. Entra a [https://developer.paypal.com](https://developer.paypal.com) y crea una cuenta gratuita (o inicia sesión con una cuenta de PayPal que ya tengas).
2. Ve a **Apps & Credentials** (aparece por defecto en modo **Sandbox**, que es lo que queremos).
3. Click en **Create App**, dale un nombre (ej. "Ecommerce Dev"), tipo **Merchant**.
4. Copia el **Client ID** y el **Secret** que te genera — los vas a necesitar en el paso 3.
5. PayPal crea automáticamente dos cuentas de sandbox de prueba: una de **negocio** (quien recibe el dinero, ya es la que usa tu app) y una **personal/comprador** (con la que vas a "comprar" para probar). Puedes verlas y ver sus credenciales de login en **Sandbox → Accounts**. Anota el email y contraseña de la cuenta **personal/comprador** — la vas a necesitar en el checklist para loguearte y aprobar el pago.

## 2. Agregar los proyectos a la solución

```bash
dotnet sln add src/Services/Payments/Payments.Domain/Payments.Domain.csproj
dotnet sln add src/Services/Payments/Payments.Application/Payments.Application.csproj
dotnet sln add src/Services/Payments/Payments.Infrastructure/Payments.Infrastructure.csproj
dotnet sln add src/Services/Payments/Payments.Api/Payments.Api.csproj
dotnet sln add src/Services/Payments/Payments.Tests/Payments.UnitTests/Payments.UnitTests.csproj
dotnet sln add src/Services/Payments/Payments.Tests/Payments.IntegrationTests/Payments.IntegrationTests.csproj
```

## 3. Correr los tests (NO necesitan credenciales reales de PayPal)

```bash
dotnet test src/Services/Payments/Payments.Tests/Payments.UnitTests
dotnet test src/Services/Payments/Payments.Tests/Payments.IntegrationTests
```

Los de integración usan un **fake** de PayPal (`FakePayPalClient`), igual
que Carrito usaba fakes de Catálogo/Inventario — así se prueba toda la
lógica de tu lado (idempotencia, transiciones de estado, manejo de
fallos) sin depender de la red ni de credenciales reales.

## 4. Levantar todo (esta vez SÍ necesitas las credenciales del paso 1)

**cmd.exe (Windows):**
```cmd
set PAYPAL_CLIENT_ID=el_client_id_que_copiaste
set PAYPAL_CLIENT_SECRET=el_secret_que_copiaste
docker compose up -d --build
```

Sin esto, `payments-service` arranca igual, pero cualquier llamada que
necesite hablar con PayPal de verdad (crear una orden) va a fallar con un
`502` — es intencional (ver `PayPalCommunicationException` en el código),
para que el error sea claro en vez de un `500` genérico.

## 5. Checklist de verificación manual

1. **Healthcheck directo:**
   ```cmd
   curl http://localhost:5005/health
   ```

2. **Loguéate** (usa tu Cliente o Admin de siempre) y guarda `%TOKEN%`.

3. **Crear un pago** (usa cualquier `ORDER_ID` — todavía no existe el módulo de Órdenes de verdad, así que por ahora es solo un Guid inventado a mano, representando una orden futura):
   ```cmd
   set ORDER_ID=22222222-2222-2222-2222-222222222222
   curl -X POST http://localhost:5000/api/payments -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"orderId\":\"%ORDER_ID%\",\"amount\":49.99,\"currency\":\"USD\"}"
   ```
   Debe devolver `200 OK` con un `approveUrl` — una URL real de `sandbox.paypal.com`.

4. **Abre el `approveUrl` en tu navegador.** Inicia sesión con la cuenta **personal/comprador** de sandbox (la del paso 1.5) y aprueba el pago. PayPal te va a redirigir a una página nuestra (`/api/payments/return`) confirmando la aprobación.

5. **Captura el pago** (el paso final, que de verdad mueve el dinero en sandbox):
   ```cmd
   curl -X POST http://localhost:5000/api/payments/%ORDER_ID%/capture -H "Authorization: Bearer %TOKEN%"
   ```
   Debe devolver `200 OK` con `"status":"Captured"` y un `payPalCaptureId`.

6. **Consultar el estado:**
   ```cmd
   curl http://localhost:5000/api/payments/%ORDER_ID% -H "Authorization: Bearer %TOKEN%"
   ```

7. **Probar la idempotencia** — repite el paso 3 con el MISMO `%ORDER_ID%`: debe devolver el mismo `paymentId` de antes, no crear uno nuevo.

### Sobre el webhook (opcional / avanzado)

El endpoint `/api/payments/webhook` funciona, pero para que PayPal te
mande webhooks reales necesita una URL **pública** (no `localhost`) — algo
como [ngrok](https://ngrok.com) para exponer tu máquina temporalmente. Si
quieres probarlo:
1. Corre `ngrok http 5000` y copia la URL pública que te da.
2. En el dashboard de PayPal (**Apps & Credentials → tu app → Add Webhook**), agrega `https://tu-url-de-ngrok.ngrok.io/api/payments/webhook`, y suscríbete al menos a `PAYMENT.CAPTURE.COMPLETED` y `PAYMENT.CAPTURE.DENIED`.
3. Copia el **Webhook ID** que te genera y agrégalo como variable de entorno `PAYPAL_WEBHOOK_ID` antes de levantar los contenedores.

Si no configuras esto, el checklist de arriba (pasos 1-7) sigue
funcionando perfecto sin el webhook — la captura manual (paso 5) es
suficiente para completar el pago. El webhook es un mecanismo de
reconciliación adicional (RF6.3), no el único camino.

---

# Módulo 7: Órdenes/Checkout (la saga)

Este es el módulo que orquesta a los otros tres: Carrito, Inventario y
Pagos. No usa eventos asíncronos (como Catálogo→Inventario) — es una
**saga orquestada**, con Órdenes llamando directamente, por HTTP, a cada
servicio y decidiendo qué hacer según el resultado. Nota importante: como
ahora Órdenes reserva stock en nombre del usuario, tuve que abrir los
endpoints de reserva de Inventario (que en el Módulo 4 dejé protegidos
por rol Admin "para poder probarlos a mano") a cualquier usuario
autenticado — revísalo si ya tenías el Módulo 4 corriendo desde antes.

## El flujo completo (por qué son DOS llamadas, no una)

Capturar un pago en PayPal necesita que el comprador apruebe en su
navegador — un paso manual que ninguna API puede saltarse. Por eso el
checkout se divide en dos:

1. **`POST /api/orders/checkout`** — reserva stock, crea la orden, crea el pago en PayPal, devuelve un `approveUrl`.
2. *(el comprador aprueba en el navegador — igual que en el Módulo 6)*
3. **`POST /api/orders/{orderId}/confirm-payment`** — captura el pago de verdad. Si sale bien: orden pagada + stock confirmado + carrito limpio. Si sale mal: orden fallida + stock liberado.

## 1. Agregar los proyectos a la solución

```bash
dotnet sln add src/Services/Orders/Orders.Domain/Orders.Domain.csproj
dotnet sln add src/Services/Orders/Orders.Application/Orders.Application.csproj
dotnet sln add src/Services/Orders/Orders.Infrastructure/Orders.Infrastructure.csproj
dotnet sln add src/Services/Orders/Orders.Api/Orders.Api.csproj
dotnet sln add src/Services/Orders/Orders.Tests/Orders.UnitTests/Orders.UnitTests.csproj
dotnet sln add src/Services/Orders/Orders.Tests/Orders.IntegrationTests/Orders.IntegrationTests.csproj
```

## 2. Correr los tests

```bash
dotnet test src/Services/Orders/Orders.Tests/Orders.UnitTests
dotnet test src/Services/Orders/Orders.Tests/Orders.IntegrationTests
```

Los unitarios son los más importantes de todo el proyecto hasta ahora:
prueban explícitamente la **compensación** de la saga — que si Pagos
falla después de reservar stock, esa reserva se libera sola; que si la
captura falla, la orden queda `Failed` y el stock vuelve a estar
disponible; que todo es idempotente ante reintentos.

También hay que volver a correr los tests de **Inventario**, porque el
cambio de autorización en sus endpoints de reserva los toca indirectamente:

```bash
dotnet test src/Services/Inventory/Inventory.Tests/Inventory.IntegrationTests
```

## 3. Levantar todo

```bash
docker compose up -d --build
```

## 4. Checklist de verificación manual

1. **Healthcheck directo:**
   ```cmd
   curl http://localhost:5006/health
   ```

2. **Prepara el terreno**: loguéate como Cliente, agrega al menos un ítem al carrito (Módulo 5) con stock suficiente ya cargado (Módulo 4). Guarda `%TOKEN%` y anota el `VARIANT_ID` que agregaste.

3. **Checkout** (el `VariantIds` es una lista — así se ve el checkout parcial: puedes tener más cosas en el carrito y solo llevar algunas al checkout):
   ```cmd
   curl -X POST http://localhost:5000/api/orders/checkout -H "Content-Type: application/json" -H "Authorization: Bearer %TOKEN%" -d "{\"variantIds\":[\"%VARIANT_ID%\"],\"shippingAddress\":\"Av. Siempre Viva 742\"}"
   ```
   Debe devolver `200 OK` con `"status":"PendingPayment"` y un `approveUrl` real de PayPal. Guarda el `orderId`:
   ```cmd
   set ORDER_ID=el_orderId_que_devolvio
   ```

4. **Verifica que el stock se reservó** (consulta Inventario directamente — `quantityAvailable` debe haber bajado, `quantityOnHand` no todavía):
   ```cmd
   curl http://localhost:5000/api/stock/%VARIANT_ID% -H "Authorization: Bearer %TOKEN%"
   ```

5. **Abre el `approveUrl` en tu navegador** y aprueba el pago con tu cuenta sandbox de comprador (si no tienes acceso a PayPal, salta este paso y el siguiente — ver la nota de abajo).

6. **Confirma el pago:**
   ```cmd
   curl -X POST http://localhost:5000/api/orders/%ORDER_ID%/confirm-payment -H "Authorization: Bearer %TOKEN%"
   ```
   Debe devolver `200 OK` con `"status":"Paid"`.

7. **Verifica el resultado en cadena:**
   - El stock (`quantityOnHand` ya bajó de verdad):
     ```cmd
     curl http://localhost:5000/api/stock/%VARIANT_ID% -H "Authorization: Bearer %TOKEN%"
     ```
   - El carrito (el ítem comprado ya no debería estar):
     ```cmd
     curl http://localhost:5000/api/cart -H "Authorization: Bearer %TOKEN%"
     ```

8. **Consultar tus órdenes:**
   ```cmd
   curl http://localhost:5000/api/orders -H "Authorization: Bearer %TOKEN%"
   ```

### Si no tienes acceso a PayPal (igual que en el Módulo 6)

Los pasos 5 y 6 de arriba requieren el navegador. Si no puedes, con que
el checklist llegue hasta el paso 4 (checkout exitoso, `approveUrl`
devuelto, stock reservado) ya confirma que la orquestación real
Carrito→Inventario→Pagos funciona — los tests automatizados (paso 2) ya
cubrieron con fakes el tramo de captura/confirmación que no puedes probar
en vivo.

## Siguiente paso

Con esto, el corazón funcional del e-commerce está completo: navegar
catálogo, armar carrito, pagar, y que el stock se descuente de verdad.
Lo que queda del plan original (Notificaciones, Panel de Admin,
Frontend Vue) son módulos de superficie sobre esta base ya sólida.

Si algo falla, copia el error exacto — si es de Docker, incluye
`docker compose logs orders-service --tail 60`.

---

# Módulo 8: Notificaciones (emails)

Un microservicio que **escucha eventos de RabbitMQ** y manda emails reales
con [Resend](https://resend.com) — mismo patrón que Catálogo→Inventario.
Los servicios que originan el evento **no esperan** al email: si Resend o
RabbitMQ fallan, el registro o el pago igual se completan.

| Evento | Publicado por | Email que se envía |
|---|---|---|
| `UserRegistered` | Users (al registrarse) | Bienvenida |
| `OrderPaid` | Órdenes (al confirmar el pago) | Confirmación de pedido con total |
| `OrderShipped` | Órdenes (`POST /api/orders/{id}/ship`, solo Admin) | "Tu pedido va en camino" |

**Garantías de diseño:** nunca se manda el mismo email dos veces (idempotencia
con restricción única en la base de datos, aun con eventos duplicados o
concurrentes); si el envío falla, MassTransit reintenta 3 veces (cada 10 s);
los nombres de usuario se escapan (HTML-encode) antes de entrar al email.

## Cambios en módulos anteriores (por qué hay que re-correr sus tests)

- **Órdenes**: nuevo estado `Shipped`; ahora guarda el email/nombre del comprador (los toma del JWT en el checkout); publica `OrderPaid`/`OrderShipped`.
- **Users**: publica `UserRegistered` al registrar.
- Ambos ahora usan RabbitMQ, así que **sus tests de integración levantan un RabbitMQ real** (Testcontainers), igual que Inventario.

## 1. Conseguir la API key de Resend (gratis, una sola vez)

1. Crea una cuenta en [resend.com](https://resend.com) (plan gratuito: 3,000 emails/mes, 100/día).
2. Ve a **API Keys → Create API Key**, permiso **Sending access**, y copia la key (empieza con `re_`). Solo se muestra una vez.
3. ⚠️ **Limitación del modo de pruebas:** sin verificar un dominio propio, el remitente `onboarding@resend.dev` **solo puede enviar al mismo email con el que te registraste en Resend**. Para probar, regístrate en la tienda usando **ese mismo email**. (Para enviar a cualquier cliente hay que verificar un dominio en Resend → Domains, y poner `RESEND_FROM_ADDRESS`.)

Si no puedes crear la cuenta, no pasa nada: los tests automatizados (con un fake del proveedor) verifican toda la lógica, y sin API key el servicio arranca igual — solo registra un error claro en los logs y reintenta.

## 2. Agregar los proyectos a la solución

```bash
dotnet sln add src/Services/Notifications/Notifications.Domain/Notifications.Domain.csproj
dotnet sln add src/Services/Notifications/Notifications.Application/Notifications.Application.csproj
dotnet sln add src/Services/Notifications/Notifications.Infrastructure/Notifications.Infrastructure.csproj
dotnet sln add src/Services/Notifications/Notifications.Api/Notifications.Api.csproj
dotnet sln add src/Services/Notifications/Notifications.Tests/Notifications.UnitTests/Notifications.UnitTests.csproj
dotnet sln add src/Services/Notifications/Notifications.Tests/Notifications.IntegrationTests/Notifications.IntegrationTests.csproj
```

## 3. Correr los tests (no necesitan cuenta de Resend)

```bash
dotnet test src/Services/Notifications/Notifications.Tests/Notifications.UnitTests
dotnet test src/Services/Notifications/Notifications.Tests/Notifications.IntegrationTests
# Módulos modificados por este cambio:
dotnet test src/Services/Users/Users.Tests/Users.UnitTests
dotnet test src/Services/Users/Users.Tests/Users.IntegrationTests
dotnet test src/Services/Orders/Orders.Tests/Orders.UnitTests
dotnet test src/Services/Orders/Orders.Tests/Orders.IntegrationTests
```

## 4. Levantar todo

**cmd.exe (Windows):**
```cmd
set RESEND_API_KEY=re_tu_api_key
docker compose up -d --build
```
(Opcional: `set RESEND_FROM_ADDRESS=tienda@tudominio.com` si verificaste un dominio.)

## 5. Checklist de verificación manual

1. **Healthcheck:**
   ```cmd
   curl http://localhost:5007/health
   ```

2. **Email de bienvenida** — registra un cliente con el **mismo email de tu cuenta de Resend**:
   ```cmd
   curl -X POST http://localhost:5000/api/auth/register -H "Content-Type: application/json" -d "{\"email\":\"TU_EMAIL_DE_RESEND\",\"password\":\"Password123\",\"fullName\":\"Tu Nombre\"}"
   ```
   En unos segundos debe llegarte el email de bienvenida (revisa spam la primera vez).

3. **Auditoría (Admin)** — lista los emails registrados como enviados:
   ```cmd
   curl http://localhost:5000/api/notifications -H "Authorization: Bearer %TOKEN%"
   ```
   Debe aparecer una entrada `UserRegistered` para ese email.

4. **Email de "pedido enviado"** (necesita una orden `Paid`, o sea el flujo de PayPal del Módulo 7):
   ```cmd
   curl -X POST http://localhost:5000/api/orders/%ORDER_ID%/ship -H "Authorization: Bearer %TOKEN%"
   ```
   (`%TOKEN%` de un **Admin**.) Sin acceso a PayPal no se puede llegar a `Paid` en vivo; el flujo completo `OrderPaid → email → Shipped → email` está cubierto por los tests de integración.

5. **Idempotencia** — repite el paso 4 sobre la misma orden: no debe llegar un segundo email.

6. **Sin API key** (opcional, para ver el manejo de errores): levanta sin `RESEND_API_KEY`, registra un usuario y mira `docker compose logs notifications-service --tail 40`: verás el error claro *"Falta la API key de Resend"* y los reintentos — y el registro del usuario habrá funcionado igual.

## Siguiente paso

Quedan los módulos de superficie sobre esta base: **Panel de Administración**
y **Frontend (Vue + Tailwind)**.

Si algo falla, incluye `docker compose logs notifications-service --tail 60`.

---

# Módulo 9: Panel de Administración (Vue + Tailwind)

Una SPA de Vue 3, compilada y servida por nginx en su propio contenedor.
Habla con los microservicios **siempre a través del Gateway** — nunca
directo a un servicio — igual que hará el frontend de clientes más adelante.

## Cómo funciona el login (dos capas, a propósito)

1. **Clave de sitio** (`VITE_ADMIN_SITE_KEY`): protege que cualquiera que
   encuentre la URL vea siquiera la pantalla de login. Se compara en el
   navegador — no es seguridad real, es una cortina.
2. **Login real** contra `/api/auth/login` (Users), igual que cualquier
   otro cliente. Si el rol no es `Admin`, se rechaza. El JWT que devuelve
   es el que de verdad autoriza cada llamada a la API.

## Qué incluye

| Sección | Qué hace |
|---|---|
| Productos | Crear productos con variantes, editar datos básicos, subir imágenes |
| Categorías | Listar y crear (con categoría padre opcional) |
| Inventario | Consultar/ajustar stock por variante, ver alertas de bajo stock |
| Órdenes | Ver todas las órdenes del sistema, marcar como enviadas |
| Administradores | Crear nuevas cuentas Admin (pide la API key de aprovisionamiento del Módulo 2) |
| Notificaciones | Auditoría de los emails enviados |

## 1. Instalar dependencias y correr en modo desarrollo (opcional, sin Docker)

```bash
cd admin-panel
corepack enable && corepack prepare pnpm@9.15.9 --activate   # una sola vez, si no tienes pnpm
pnpm install
pnpm run dev
```

Abre `http://localhost:5174`. Necesitas el Gateway y los demás servicios
corriendo (`docker compose up -d`) para que las llamadas a la API funcionen.

## 2. Levantar todo con Docker (recomendado)

```cmd
docker compose up -d --build
```

El panel queda en **http://localhost:8081**.

## 3. Checklist de verificación manual

1. Abre `http://localhost:8081` — debe pedir la clave de sitio (`clave-panel-de-desarrollo` por defecto, o la que hayas puesto en `ADMIN_PANEL_SITE_KEY`).
2. Después de la clave, inicia sesión con un usuario **Admin** real (el que creaste en el Módulo 2, ej. `admin@test.com` / `Password123`).
3. **Categorías**: crea una categoría nueva, confirma que aparece en la tabla.
4. **Productos**: crea un producto nuevo con al menos una variante (SKU + precio). Al guardar te lleva a "editar" — sube una imagen ahí.
5. **Inventario**: pega el `Id` de la variante que acabas de crear en el buscador, ajusta la cantidad en mano, confirma que se actualiza.
6. **Órdenes**: si tienes alguna orden en estado `Paid` (Módulo 7), debe aparecer aquí con el botón "Marcar enviada".
7. **Administradores**: crea un segundo Admin (necesitas la `ADMIN_PROVISIONING_KEY` del `docker-compose.yml`).
8. **Notificaciones**: debe listar los emails que ya se enviaron en el Módulo 8.

## Mejora: buscar producto/variante en Inventario (sin DevTools)

Antes, para ajustar el stock de una variante había que copiar su Id desde
las herramientas de desarrollador del navegador — nada intuitivo. Ahora
Inventario tiene un buscador real: escribes el nombre del producto, eliges
uno de los resultados, y eliges la variante (SKU + atributos + precio,
nada de GUIDs) — eso dispara la consulta de stock sola. El campo de Id
directo sigue existiendo como opción avanzada, colapsado detrás de "O
pega el Id de la variante directamente".

También se puede llegar a Inventario con una variante ya preseleccionada
desde la pantalla de edición de un producto (botón "Ver/ajustar stock" en
cada fila de la tabla de variantes).

## Mejora: imágenes reales y búsqueda avanzada en el storefront

- **Imágenes reales**: el backend de Catálogo ya tenía todo esto construido
  desde el Módulo 3 (subida de imágenes en el panel de Admin, servidas en
  `/images/{fileName}` a través del Gateway) — pero el storefront nunca las
  mostraba, siempre ponía un emoji de relleno. Ahora el catálogo y el
  detalle de producto muestran la imagen real (la marcada como principal
  primero), con el emoji solo como respaldo cuando el producto no tiene
  ninguna imagen subida. El detalle de producto además muestra una tira de
  miniaturas cuando hay más de una imagen.
- **Ordenar por precio o más reciente**: nuevo parámetro `sortBy` en
  `GET /api/products` (`name` por defecto, `price_asc`, `price_desc`,
  `newest`), con su propio test de integración.
- **Filtrar por rango de precio**: esto ya existía en el backend
  (`minPrice`/`maxPrice`) desde antes, solo nunca estuvo expuesto en el
  storefront — ahora hay dos campos para eso en el catálogo.

## Tests de los frontends (Vitest + Playwright)

Además de los tests de los 8 microservicios (unitarios + integración, ya
cubiertos desde el Módulo 2), el panel de Admin y el storefront tienen su
propia cobertura.

### Unitarios (Vitest) — no necesitan nada corriendo

Prueban los stores de Pinia (`auth`, `cart`) y el cliente HTTP, en
aislamiento. Corren en milisegundos.

```bash
cd admin-panel && pnpm install && pnpm run test:unit
cd ../storefront && pnpm install && pnpm run test:unit
```

### End-to-end (Playwright) — necesitan el stack completo corriendo

Prueban el flujo real contra los 20 servicios: login en dos capas del
panel, catálogo, carrito, checkout completo con PayPal simulado, gestión
de categorías/productos/inventario, etc. Un `globalSetup` siembra sus
propios datos de prueba (categoría, producto, variante, stock, un Admin)
vía la API antes de correr — no dependen de que ya hayas creado nada a mano.

```bash
# 1. Asegúrate de tener PAYPAL_PROVIDER=Fake en tu .env (si no, el test de
#    checkout va a fallar con el mismo 502 esperado de siempre).
docker compose up -d --build

# 2. En otra terminal:
cd e2e
pnpm install
pnpm exec playwright install --with-deps chromium
pnpm test
```

`pnpm run test:ui` (en vez de `pnpm test`) abre la interfaz visual de
Playwright, útil para ver paso a paso qué está haciendo cada test.

### En GitHub Actions (automático)

`.github/workflows/ci.yml` corre los tres niveles en cada push/PR a
`main`/`master`: backend (.NET), frontends (Vitest), y end-to-end
(Playwright, levantando el stack completo en Docker con `PAYPAL_PROVIDER=Fake`
— sin necesitar ningún secreto real). Si un test end-to-end falla, el
workflow sube el reporte HTML de Playwright como artefacto descargable.

## Configurar las claves una sola vez (`.env`), en vez de `set` en cada terminal

A lo largo de este README vas a ver comandos como `set RESEND_API_KEY=...`
antes de `docker compose up`. Eso funciona, pero **solo dura mientras esa
ventana de `cmd` siga abierta** — si abres una terminal nueva, o corres
`docker compose up --build` de otro servicio en una ventana donde nunca
hiciste el `set`, Docker recrea ese contenedor **sin** la variable, y
vuelves a ver el mismo error de "falta la API key" que ya resolviste antes
(nos pasó de verdad con Resend).

La forma correcta de evitar esto: copia `.env.example` a `.env` (sin el
`.example`) en la raíz del repo, y completa ahí tus valores reales una
sola vez.

```cmd
copy .env.example .env
notepad .env
```

Docker Compose lee `.env` automáticamente en cada `docker compose up` —
nunca más hace falta `set`. El archivo `.env` real **nunca se sube a
git** (ya está en `.gitignore`); solo `.env.example` (sin valores reales)
se commitea, como plantilla para quien clone el repo después.

## Probar el checkout completo sin PayPal real (modo simulado, opcional)

Si no tienes acceso a una cuenta de PayPal (por región, o cualquier otro
motivo), puedes probar el flujo completo — pagar → orden `Paid` → marcar
`Shipped` desde el panel de Admin → emails de confirmación y de envío —
sin salir de tu máquina. Pagos tiene un `IPayPalClient` simulado
(`FakePayPalClient`), la misma interfaz que ya usan los tests
automatizados, activable solo por configuración. **Por defecto siempre
usa el PayPal real** — esto nunca se activa solo.

```cmd
set PAYPAL_PROVIDER=Fake
docker compose up -d --build payments-service
```

Con esto activo, al hacer checkout desde el storefront:
- No se abre ninguna pestaña de PayPal (`approveUrl` viene vacío a propósito).
- En la pantalla de espera (`/orders/{id}/pending`), el botón "Ya aprobé el pago — confirmar" captura el pago simulado — siempre exitoso.
- La orden queda `Paid` de verdad en tu base de datos, dispara el email de confirmación (Módulo 8), y desde el panel de Admin puedes marcarla `Shipped` y ver el segundo email.

Para volver a PayPal real: `set PAYPAL_PROVIDER=Real` (o simplemente no
definas la variable) y reconstruye `payments-service` de nuevo.
**Nunca actives esto en producción** — un pago "simulado" nunca mueve
dinero de verdad, aunque la orden quede marcada como pagada.

## Troubleshooting general: `no such host` / `dial tcp` al construir imágenes

Si `docker compose up --build` falla con algo como:
```
failed to resolve source metadata for ...: dialing registry-1.docker.io:443 ...: no such host
```
(o lo mismo con `mcr.microsoft.com`, `deb.nodesource.com`, o al descargar paquetes de NuGet/npm a mitad de un build) — **no es un bug del proyecto**, es Docker Desktop perdiendo resolución DNS dentro de WSL2, algo intermitente y conocido. Ya nos pasó antes con timeouts de NuGet.

**Primero, lo más simple**: reintenta el mismo comando una o dos veces — Docker reusa todo lo que ya se cacheó, así que un reintento suele completar solo la parte que falló.

**Si se repite seguido**, el arreglo de fondo es forzar el modo de red "mirrored" de WSL2:
1. Crea (o edita) `C:\Users\<tu_usuario>\.wslconfig`:
   ```ini
   [wsl2]
   networkingMode=mirrored
   ```
2. Cierra Docker Desktop.
3. `wsl --shutdown` (en cmd.exe o PowerShell).
4. Vuelve a abrir Docker Desktop y espera a que arranque del todo.
5. Reintenta `docker compose up -d --build`.

## Troubleshooting general: `.dockerignore` — solo el de la raíz cuenta

Todos los servicios usan `context: .` (la raíz del repo) en `docker-compose.yml`,
`admin-panel` y `storefront` incluidos. **Docker solo lee el `.dockerignore` que
está en la raíz del contexto de build** — uno dentro de una subcarpeta (ej.
`storefront/.dockerignore`) se ignora en silencio, sin ningún error. Por eso
existe un único `.dockerignore` en la raíz del repo, con reglas para .NET
(`**/bin`, `**/obj`) y Node (`**/node_modules`, `**/dist`) a la vez. Si algún
día agregas un servicio nuevo y sus artefactos de build locales empiezan a
"colarse" en la imagen (errores raros de módulos no encontrados, conflictos
de tipo "cannot replace directory with file", contextos de build
sospechosamente grandes en el log de `docker compose up --build`), revisa
que el patrón esté cubierto acá, no crees un `.dockerignore` nuevo en la
subcarpeta — no serviría de nada.

## Troubleshooting general: `column ... does not exist` (esquema desactualizado)

Todo el proyecto usa `EnsureCreatedAsync()` en vez de migraciones reales de
EF Core (velocidad de desarrollo, a costa de esto). **`EnsureCreated` solo
crea las tablas la primera vez que la base de datos no existe — si ya
existe, nunca la actualiza**, aunque el código C# haya cambiado. Esto pasó
de verdad en el Módulo 9: agregamos `user_email`/`user_full_name` a
`Order` en el Módulo 8, pero como `postgres-orders` ya tenía datos de
antes, Postgres nunca se enteró.

**Si ves un error `column X does not exist` en los logs de cualquier
servicio** después de que le agregamos un campo nuevo a una entidad, es
esto. Dos formas de arreglarlo:

- **Agregar la columna a mano** (conserva los datos existentes):
  ```cmd
  docker exec -it ecommerce-postgres-<servicio> psql -U <usuario>_svc -d <servicio>_db -c "ALTER TABLE <tabla> ADD COLUMN IF NOT EXISTS <columna> <tipo>;"
  ```
- **Reiniciar el volumen de ese Postgres** (pierdes los datos de esa tabla, pero es más simple si no te importa perder los de prueba):
  ```cmd
  docker compose down
  docker volume rm ecommerce_pg_<servicio>_data
  docker compose up -d --build
  ```

## Siguiente paso

Queda un solo módulo del plan original: el **Frontend de clientes** (Vue +
Tailwind) — navegar el catálogo, armar el carrito, y hacer checkout, todo
desde una interfaz real en vez de `curl`.

Si algo falla, incluye `docker compose logs admin-panel --tail 60` (poco
probable, es solo nginx sirviendo archivos estáticos) o abre la consola
del navegador (F12) para ver el error real de la llamada a la API.

---

# Módulo 10: Frontend de clientes (Vue + Tailwind)

El último módulo del plan original. Una SPA de Vue 3, mismo patrón que el
panel de Admin (pnpm, Vite, Tailwind, Pinia, nginx en Docker) — pero
orientada al comprador, no al administrador.

## Qué incluye

| Sección | Qué hace |
|---|---|
| Catálogo | Buscar/filtrar productos por categoría, ver el detalle con sus variantes |
| Cuenta | Registrarse e iniciar sesión (el access token se renueva solo cuando expira, con el refresh token) |
| Carrito | Agregar, editar cantidades, quitar ítems, elegir cuáles llevar al checkout |
| Checkout | Dirección de envío + pagar con PayPal (mismo flujo del Módulo 7: se abre una pestaña de PayPal, y al volver hay que confirmar) |
| Mis pedidos | Historial de las propias órdenes, con el detalle de cada una |

## 1. Instalar y correr en modo desarrollo (opcional, sin Docker)

`corepack` activa `pnpm` para **todo tu sistema**, no por carpeta — si ya lo
hiciste en el Módulo 9, `pnpm --version` ya te va a funcionar acá también,
no hace falta repetirlo (y si lo repites en una terminal sin permisos de
Administrador, en Windows puede darte `EPERM` al intentar escribir en
`C:\Program Files\nodejs\` — ignóralo si `pnpm --version` ya responde).

```bash
cd storefront
pnpm --version   # si no imprime nada, corre 'corepack enable && corepack prepare pnpm@9.15.9 --activate' como Administrador
pnpm install
pnpm run dev
```

Abre `http://localhost:5173`.

## 2. Levantar todo con Docker

```cmd
docker compose up -d --build
```

## 3. Checklist de verificación manual

1. Abre `http://localhost:5173` — debe verse la grilla de productos (los que ya creaste desde el panel de Admin).
2. Busca por nombre y filtra por categoría — confirma que la grilla se actualiza.
3. Entra a un producto, elige variante y cantidad, "Agregar al carrito" — como no estás logueado, te manda a `/login`.
4. **Regístrate** con un usuario nuevo (o inicia sesión con uno existente).
5. Vuelve al producto y agrégalo de verdad — el número en el ícono del carrito del header debe actualizarse.
6. Ve a **Carrito**, ajusta una cantidad, quita otro ítem si tienes varios, confirma que el subtotal se recalcula.
7. Selecciona los ítems que quieres comprar (checkbox) y dale a "Continuar al checkout".
8. Llena la dirección de envío y dale a "Pagar con PayPal" — se abre una pestaña nueva con el `approveUrl` real.
9. Si tienes acceso a PayPal: aprueba el pago ahí, vuelve a la pestaña del storefront, y dale a "Ya aprobé el pago — confirmar". Si no tienes acceso a PayPal (ver Módulo 6), este paso va a fallar con el mismo `502` de siempre — es esperado, no un bug nuevo.
10. Ve a **Mis pedidos** — debe listar la orden que acabas de crear, con su estado real.

## Con esto, el plan original de 10 módulos queda completo

Infraestructura → Users → Catálogo → Inventario → Carrito → Pagos →
Órdenes → Notificaciones → Panel de Admin → Frontend de clientes. Todo
con tests automatizados, verificado manualmente donde fue posible, y con
cada bug real que apareció en el camino documentado en este README.

Si algo falla, incluye `docker compose logs storefront --tail 60` (poco
probable, es solo nginx) o la consola del navegador (F12) para el error
real de la llamada a la API.

---

# Fase de crecimiento: funcionalidades inspiradas en el mercado

Después de los 10 módulos originales se analizaron 25 plataformas de comercio electrónico
(ver [`docs/ANALISIS-MERCADO.md`](docs/ANALISIS-MERCADO.md)) y se armó un roadmap por fases
(ver [`docs/ROADMAP-FUNCIONALIDADES.md`](docs/ROADMAP-FUNCIONALIDADES.md)). Cada tarea se
construye con **las pruebas escritas primero** y respetando la arquitectura: un bounded context
por microservicio, su propia base de datos, eventos por RabbitMQ y todo detrás del Gateway.

> **¿Primera vez probando la app?** Sigue la [guía de pruebas en local](docs/GUIA-PRUEBAS-LOCALES.md):
> levantar todo, cargar datos de demostración y recorrer cada funcionalidad como cliente y como Admin.

## Fase 0 — Identidad visual (storefront)

- **Tokens de diseño semánticos** (`bg-surface`, `text-ink`, `border-line`, `text-brand-ink`...):
  cambian solos entre modo claro y oscuro mediante variables CSS, así ninguna vista necesita
  variantes `dark:` para sus superficies y textos.
- **Modo oscuro** con botón en el encabezado; respeta la preferencia del sistema la primera vez y
  recuerda la elección. Un script en `index.html` lo aplica antes de pintar (sin parpadeo).
- **Encabezado** con buscador global (`?q=`), carrito con contador y menú de usuario; **portada**
  con banner; tarjetas de producto unificadas; esqueletos de carga; avisos (toasts); pie de página.
- **Nombre y lema configurables** en `.env`, aplicados al reconstruir el storefront:
  ```
  STORE_NAME=Mi Tienda
  STORE_TAGLINE=Lo mejor para tu casa
  ```
  ```cmd
  docker compose up -d --build storefront
  ```

## Fase 1 — Reseñas y calificaciones (servicio `Reviews`, puerto 5008)

- Una reseña por cliente y producto (índice UNIQUE en la base: es la defensa real ante dos
  requests simultáneos), calificación 1–5, título (máx. 100) y comentario opcional (máx. 2000).
- El nombre se muestra protegido (**"Ana P."**) y la API pública **nunca** expone el id del usuario.
- **Compra verificada**: Órdenes publica `OrderPaidEvent` con los `ProductIds` comprados y Reseñas
  lo consume. Si el cliente ya había reseñado antes de comprar, su reseña pasa a verificada.
- Endpoints (todos por el Gateway, `http://localhost:5000`):

  | Método | Ruta | Acceso |
  |---|---|---|
  | GET | `/api/reviews/products/{id}?sort=newest\|highest\|lowest&page=&pageSize=` | público |
  | GET | `/api/reviews/products/{id}/summary` | público |
  | GET | `/api/reviews/summaries?productIds=a&productIds=b` | público (hasta 100) |
  | GET | `/api/reviews/products/{id}/mine` | cliente (404 si aún no reseñó) |
  | POST | `/api/reviews/products/{id}` | cliente (409 si ya reseñó) |
  | PUT | `/api/reviews/{reviewId}` | autor |
  | DELETE | `/api/reviews/{reviewId}` | autor o **Admin** (moderación) |

- **Cola propia por servicio**: el consumidor de Reseñas usa el prefijo `reviews-`
  (`reviews-order-paid`). Si usara el mismo nombre que el de Notificaciones, ambos compartirían una
  cola y RabbitMQ le entregaría cada evento a *uno solo* de los dos. Hay una prueba que lo protege.
- Compatibilidad: `OrderPaidEvent.ProductIds` es un campo **opcional al final**; los consumidores
  y eventos anteriores siguen funcionando.

### Cómo probarlo

```cmd
:: Servicios nuevos (Postgres + Reseñas) y el Gateway con su nueva ruta:
docker compose up -d --build reviews-service postgres-reviews gateway storefront

:: Pruebas del servicio (Docker requerido: levantan Postgres y RabbitMQ reales)
dotnet test src\Services\Reviews\Reviews.Tests\Reviews.UnitTests
dotnet test src\Services\Reviews\Reviews.Tests\Reviews.IntegrationTests

:: Cambió el contrato compartido y Órdenes: vuelve a correr sus pruebas
dotnet test src\Services\Orders\Orders.Tests\Orders.UnitTests
dotnet test src\Services\Notifications\Notifications.Tests\Notifications.UnitTests

:: Frontend
cd storefront && pnpm install && pnpm run test:unit
cd ..\e2e && pnpm test
```

Checklist manual: abre un producto → verás **"Opiniones de clientes"**. Como invitado se ve la
invitación a iniciar sesión; con sesión, el formulario (estrellas con mouse y con las flechas del
teclado). Publica, edita y elimina (la eliminación pide confirmación). En la portada las tarjetas
muestran estrellas y cantidad. Compra el producto (PayPal simulado) y vuelve: tu reseña tendrá la
insignia **Compra verificada**.

> **Si ya tenías datos locales**: el servicio crea su esquema solo (`EnsureCreated`) en su propia
> base nueva, no hay nada que migrar.

> **Contenedores**: ahora son **20** (se suman `postgres-reviews` y `reviews-service`).

## Fase 2 — Favoritos (servicio `Wishlist`, puerto 5009)

- **Corazón** en cada tarjeta del catálogo y en el detalle del producto. Se actualiza al instante
  (optimista) y, si el servidor falla, vuelve a su estado anterior y muestra el motivo. Un invitado
  que lo toca va a iniciar sesión y vuelve a la misma página.
- **Mis favoritos** (`/favorites`, enlace con contador en el encabezado): del más reciente al más
  antiguo, con imagen y precio actuales del catálogo. Si un producto se eliminó, aparece como
  *"ya no disponible"* y se puede quitar.
- **Mover al carrito**: con un clic si el producto tiene una sola variante activa; si tiene varias
  (talla, color...), el botón es *"Elegir opción"* y lleva al detalle. El carrito valida el stock: si
  no alcanza, el favorito se queda donde estaba y se muestra el motivo.
- **Idempotente de verdad**: la clave primaria es `(usuario, producto)` y el alta usa
  `INSERT ... ON CONFLICT DO NOTHING`, así que un doble clic (o 8 requests a la vez, hay una prueba)
  deja un solo favorito y nunca responde error. Quitar algo que no está también responde 204.
- **Privado**: ningún endpoint recibe un id de usuario; siempre se toma del token.
- **Tope de 100 favoritos** por cliente (409 al pasarse). Repetir uno que ya está nunca choca con el tope.
- Guarda **solo el id del producto**, no una copia de su nombre o precio: así nunca muestra datos
  viejos. No usa RabbitMQ (no publica ni consume eventos).
- Endpoints (todos requieren sesión):

  | Método | Ruta | Respuesta |
  |---|---|---|
  | GET | `/api/wishlist` | `[{ productId, addedAtUtc }]`, del más reciente al más antiguo |
  | PUT | `/api/wishlist/{productId}` | 204 (idempotente); 409 si la lista está llena |
  | DELETE | `/api/wishlist/{productId}` | 204 (aunque no estuviera) |

### Cómo probarlo

```cmd
:: Servicios nuevos (Postgres + Favoritos) y el Gateway con su nueva ruta:
docker compose up -d --build wishlist-service postgres-wishlist gateway storefront

:: Pruebas del servicio (Docker requerido: levantan un Postgres real)
dotnet test src\Services\Wishlist\Wishlist.Tests\Wishlist.UnitTests
dotnet test src\Services\Wishlist\Wishlist.Tests\Wishlist.IntegrationTests

:: Frontend y end-to-end
cd storefront && pnpm install && pnpm run test:unit
cd ..\e2e && pnpm test
```

Checklist manual: con sesión iniciada, toca el corazón de una tarjeta (se rellena y aparece un
aviso); el contador del encabezado sube. Recarga: sigue marcado. Abre **Mis favoritos** y usa
**Mover al carrito**: desaparece de la lista y el contador del carrito sube. Cierra sesión: los
corazones quedan vacíos.

> **Contenedores**: ahora son **22** (se suman `postgres-wishlist` y `wishlist-service`).

## Fase 3 — Cupones y promociones (servicio `Promotions`, puerto 5010)

- **Tipos**: porcentaje (1–90 %, con tope opcional en dinero, ej. "20 % hasta $50") o monto fijo.
  Un monto fijo igual o mayor que la compra se rechaza: una orden de $0 no se puede cobrar en PayPal.
- **Reglas**: vigencia (inicio/fin), compra mínima, límite de usos totales, un uso por cliente y
  activo/pausado. Los mensajes de rechazo son para el cliente ("Este cupón requiere una compra
  mínima de $80.00 (tu compra: $45.00)").
- **Canje atómico dentro de la saga de Órdenes**, con el mismo ciclo que la reserva de stock:
  1. *Checkout*: Inventario reserva el stock → **Promociones reserva un uso del cupón** (recalcula el
     descuento con el subtotal real; no confía en el que mostró el navegador) → Pagos cobra el
     **total con descuento**. Si algo falla, se liberan el cupón y el stock.
  2. *Pago capturado*: se confirma el uso. *Pago rechazado*: se libera y vuelve a estar disponible.
- **Límite de usos sin carreras**: al reservar, la fila del cupón se bloquea (`SELECT ... FOR UPDATE`)
  mientras se cuentan los usos. Hay una prueba con **10 checkouts simultáneos sobre un cupón de 1 uso:
  gana exactamente uno**.
- Una reserva **abandonada** (el cliente no terminó de pagar) deja de contar a las 2 horas, para que
  no bloquee un cupón limitado para siempre.
- **Ruta interna**: el canje vive en `/internal/redemptions`, que el Gateway **no** publica. Un cliente
  no puede apartar usos de un cupón limitado llamando directo; solo Órdenes lo hace, con el token del
  cliente (cada uso queda ligado a ese usuario).
- **Storefront**: campo "¿Tienes un cupón?" en el checkout con el desglose subtotal / descuento /
  total. *Mis pedidos* y la confirmación de pago muestran el ahorro.
- **Panel de Admin**: nueva sección **Cupones** para crear, editar, pausar y ver usos y estado
  (Activo, Programado, Vencido, Agotado, Pausado).

  | Método | Ruta | Acceso |
  |---|---|---|
  | GET | `/api/coupons/validate?code=&subtotal=` | cliente (solo consulta, no aparta nada) |
  | GET | `/api/coupons` | Admin |
  | POST | `/api/coupons` | Admin (409 si el código ya existe) |
  | PUT | `/api/coupons/{id}` | Admin (el código no cambia) |
  | POST | `/internal/redemptions` · `/{orderId}/confirm` · `/{orderId}/release` | solo Órdenes (no pasa por el Gateway) |

- **Órdenes** guarda `coupon_code` y `discount_amount`. Si ya tenías una base de Órdenes de antes, el
  servicio agrega esas dos columnas solo al arrancar (no hace falta borrar nada).

### Cómo probarlo

```cmd
:: Servicios nuevos/cambiados: Promociones, Órdenes, Gateway y los dos frontends
docker compose up -d --build promotions-service postgres-promotions orders-service gateway storefront admin-panel

:: Pruebas (Docker requerido)
dotnet test src\Services\Promotions\Promotions.Tests\Promotions.UnitTests
dotnet test src\Services\Promotions\Promotions.Tests\Promotions.IntegrationTests
dotnet test src\Services\Orders\Orders.Tests\Orders.UnitTests
dotnet test src\Services\Orders\Orders.Tests\Orders.IntegrationTests
```

Checklist manual: en el panel de Admin → **Cupones**, crea `PRUEBA10` (10 %). En la tienda agrega algo
al carrito, ve al checkout y aplica `prueba10`: verás el descuento y el total nuevo. Paga (PayPal
simulado) y revisa *Mis pedidos*. En el panel, el cupón pasa a "1" uso.

> **Contenedores**: ahora son **24** (se suman `postgres-promotions` y `promotions-service`).

## Fase 4 — Descubrimiento (Catálogo e Inventario)

- **Sugerencias mientras se escribe** en el buscador del encabezado (desde 2 letras): imagen, categoría
  y precio. Accesible con teclado (↓ ↑ para moverse, Enter para abrir, Esc para cerrar) y para lectores
  de pantalla (patrón *combobox*). Enter sin elegir una sugerencia busca lo escrito, como antes.
- **Búsqueda sin tildes**: "audifonos" encuentra "Audífonos" y "cafe" encuentra "Café" (extensión
  `unaccent` de PostgreSQL, que Catálogo instala sola al arrancar). Aplica también a la búsqueda normal.
- **"También te puede interesar"** en el detalle: productos de la misma categoría y, si no alcanzan, de
  las categorías hermanas (mismo padre).
- **"Vistos recientemente"** en la portada y en el detalle. Se guarda solo en el navegador (sin backend),
  hasta 12 productos, con un botón para borrar el historial.
- **Insignia "¡Quedan solo N!" / "Agotado"** calculada con el stock **disponible real** (físico menos lo
  reservado en checkouts en curso), visible también sin iniciar sesión. Con stock de sobra no se muestra
  nada ni se expone la cantidad exacta. Agotado deshabilita "Agregar al carrito".
- **Corrección**: la tienda mostraba productos **desactivados** en la búsqueda. Ahora solo los ve el panel
  de Admin (con `includeInactive=true`, que se ignora si quien lo pide no es Admin).

  | Método | Ruta | Acceso |
  |---|---|---|
  | GET | `/api/products/suggestions?q=&limit=` | público (hasta 10) |
  | GET | `/api/products/{id}/related?limit=` | público (hasta 20) |
  | GET | `/api/stock/availability?variantIds=a&variantIds=b` | público (hasta 100) |

Para probarlo paso a paso, ver la [guía de pruebas en local](docs/GUIA-PRUEBAS-LOCALES.md) (sección 4.1 y 4.2).

## Datos de demostración

Para probar con una tienda "viva" en vez de productos con códigos raros:

```cmd
docker compose up -d
node scripts\seed-demo-data.mjs
```

Carga, a través del Gateway (como lo haría un Admin):

- **23 categorías** (Tecnología › Celulares, Moda › Calzado...) y **105 productos reales** de marcas
  conocidas (iPhone 15, Galaxy S24, Air Force 1, Levi's 501, KitchenAid, LEGO, Cien años de soledad...),
  con descripción en español, precio de referencia en USD, **181 variantes** (talla, color, capacidad),
  stock (algunos con pocas unidades y unos pocos agotados) e imagen.
- **10 cupones**: `BIENVENIDA10`, `AHORRA5`, `OTONO20`, `TECH50`, `FLASH30` (10 usos), `VIP15`,
  `MASCOTAS12`, `BLACKFRIDAY` (programado para el próximo Black Friday), `VERANO15` (vencido) y
  `NAVIDAD25` (pausado), para ver todos los estados en el panel.
- **10 clientes** y **344 reseñas** en español con calificaciones realistas.

Es **idempotente**: lo que ya existe se deja como está, así que se puede correr de nuevo sin duplicar.
Al terminar imprime los accesos (Admin `admin@demo-tienda.test` / `Admin12345!`; clientes
`ana.martinez@demo-tienda.test` y otros, todos con `Demo12345!`).

> Las imágenes son ilustraciones generadas (marca, nombre y un ícono del tipo de producto), no fotos
> oficiales de las marcas: se pueden reemplazar desde el panel de Admin. Los datos fuente están en
> `scripts/seed-data/` (`catalog.json`, `reviews.json`, `images/`).

