# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Los paquetes NuGet se guardan en una caché de BuildKit compartida por todos los servicios:
# se descargan una sola vez y sobreviven aunque cambie la imagen base o se reconstruya todo.
# "locked" hace que las restauraciones vayan de a una (sin pisarse ni saturar la red).
# Reintentos ante cortes de red/DNS al bajar de nuget.org:
ENV NUGET_ENABLE_ENHANCED_HTTP_RETRY=true \
    NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT=8 \
    NUGET_ENHANCED_NETWORK_RETRY_DELAY_MILLISECONDS=3000

COPY src/Services/Cart/Cart.Domain/Cart.Domain.csproj src/Services/Cart/Cart.Domain/
COPY src/Services/Cart/Cart.Application/Cart.Application.csproj src/Services/Cart/Cart.Application/
COPY src/Services/Cart/Cart.Infrastructure/Cart.Infrastructure.csproj src/Services/Cart/Cart.Infrastructure/
COPY src/Services/Cart/Cart.Api/Cart.Api.csproj src/Services/Cart/Cart.Api/

RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/Cart/Cart.Api/Cart.Api.csproj

COPY src/Services/Cart/ src/Services/Cart/
RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,readonly \
    dotnet publish src/Services/Cart/Cart.Api/Cart.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cart.Api.dll"]
