# Subir este proyecto a GitHub y crear los issues

Todo el trabajo ya está commiteado en un repositorio git local (7 commits,
uno por módulo). Aquí está cómo llevarlo a GitHub — todo corre en **tu
máquina**, con **tu propia sesión** de GitHub, nunca con credenciales
manejadas por Claude.

## 1. Crear el repositorio en GitHub (vacío, sin README/licencia)

Ve a [github.com/new](https://github.com/new), ponle un nombre (ej.
`ecommerce-microservicios`), y **no marques** "Add a README file" ni
".gitignore" ni licencia — el repo tiene que quedar completamente vacío,
porque ya traemos todo el historial nosotros.

## 2. Traer el historial de commits a tu máquina

Descarga `ecommerce-repo.bundle` (te lo entregué junto con estos
archivos) y guárdalo en una carpeta nueva, por ejemplo
`C:\Users\darie\Documents\ecommerce-github\`.

```cmd
cd C:\Users\darie\Documents\ecommerce-github
git clone ecommerce-repo.bundle ecommerce
cd ecommerce
```

Esto te deja una carpeta `ecommerce\` con los 7 commits ya listos,
apuntando temporalmente al archivo `.bundle` como si fuera un "remoto".

## 3. Conectarlo a tu GitHub real y subirlo

```cmd
git remote remove origin
git remote add origin https://github.com/TU_USUARIO/ecommerce-microservicios.git
git branch -M main
git push -u origin main
```

Te va a pedir loguearte (si no tienes Git Credential Manager configurado,
usa un [Personal Access Token](https://github.com/settings/tokens) como
contraseña, no tu contraseña normal de GitHub).

Listo — ya deberías ver los 7 commits en GitHub, cada uno con el código
de su módulo correspondiente.

## 4. Crear los issues (opcional, pero lo pediste)

Necesitas el [GitHub CLI](https://cli.github.com/) instalado una sola vez:

```cmd
winget install --id GitHub.cli
```

Luego, **desde dentro de la carpeta del repo** (`ecommerce\`):

```cmd
gh auth login
```

Sigue las instrucciones (elige GitHub.com, HTTPS, e inicia sesión con tu
navegador — de nuevo, tu sesión, no la mía). Una vez logueado, corre el
script que ya está incluido en el repo:

```cmd
bash create-github-issues.sh
```

(Si no tienes `bash` en Windows, puedes correrlo desde **Git Bash**, que
se instala junto con Git para Windows, o desde WSL.)

Esto crea 7 issues — uno por módulo — y los cierra automáticamente al
crearlos, con un comentario confirmando que están completados y
verificados. Si prefieres crearlos a mano en vez de con el script, los
títulos y descripciones están dentro de `create-github-issues.sh` — cópialos
directamente.

## De ahora en adelante

Cada vez que sigamos construyendo (Módulo 8 en adelante), yo te voy a
seguir entregando zips como hasta ahora — pero puedes copiar los archivos
nuevos directamente sobre tu carpeta `ecommerce\` (la que ya está
conectada a GitHub) y simplemente correr:

```cmd
git add .
git commit -m "Modulo 8: <lo que corresponda>"
git push
```

Así tu repo de GitHub queda tan actualizado como tu carpeta local, sin que
yo necesite tocar tus credenciales en ningún momento.

## Actualizar tu repo con los módulos nuevos (Módulo 8 en adelante)

Cada vez que te entregue un `ecommerce-repo.bundle` actualizado, **conserva
el historial** (los commits anteriores tienen exactamente los mismos hashes),
así que basta con traer solo lo nuevo. Desde dentro de tu carpeta `ecommerce\`:

```cmd
git pull C:\ruta\al\nuevo\ecommerce-repo.bundle master
git push
```

(Si tu rama local se llama `main`, git lo resuelve solo al hacer el pull;
si te pide elegir, usa `git pull <ruta-al-bundle> master:main`.) Después,
para el issue del Módulo 8:

```cmd
bash create-github-issue-modulo8.sh
```

Ese issue se crea **abierto** — ciérralo con `gh issue close <numero>` cuando
hayas verificado los tests y el checklist.

