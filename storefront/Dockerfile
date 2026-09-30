# Contexto de build esperado: raíz del repositorio (storefront/).

FROM node:20-alpine AS build
WORKDIR /app

RUN corepack enable && corepack prepare pnpm@9.15.9 --activate

COPY storefront/package.json storefront/pnpm-lock.yaml storefront/.npmrc ./
RUN pnpm install --frozen-lockfile

COPY storefront/ .

ARG VITE_API_BASE_URL=http://localhost:5000
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN pnpm run build

FROM nginx:1.27-alpine AS runtime
COPY storefront/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
