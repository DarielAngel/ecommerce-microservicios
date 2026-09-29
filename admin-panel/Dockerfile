# Contexto de build esperado: raíz del repositorio (admin-panel/).

FROM node:20-alpine AS build
WORKDIR /app

# corepack ya viene con Node 20; esto activa pnpm sin tener que instalarlo aparte.
RUN corepack enable && corepack prepare pnpm@9.15.9 --activate

COPY admin-panel/package.json admin-panel/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY admin-panel/ .

# Estas variables quedan HORNEADAS en el bundle JS en tiempo de build (Vite las reemplaza
# como texto literal) — no son secretos del servidor, son configuración del propio frontend.
ARG VITE_API_BASE_URL=http://localhost:5000
ARG VITE_ADMIN_SITE_KEY=clave-panel-de-desarrollo
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_ADMIN_SITE_KEY=$VITE_ADMIN_SITE_KEY

RUN pnpm run build

FROM nginx:1.27-alpine AS runtime
COPY admin-panel/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
