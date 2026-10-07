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

COPY src/Services/Wishlist/Wishlist.Domain/Wishlist.Domain.csproj src/Services/Wishlist/Wishlist.Domain/
COPY src/Services/Wishlist/Wishlist.Application/Wishlist.Application.csproj src/Services/Wishlist/Wishlist.Application/
COPY src/Services/Wishlist/Wishlist.Infrastructure/Wishlist.Infrastructure.csproj src/Services/Wishlist/Wishlist.Infrastructure/
COPY src/Services/Wishlist/Wishlist.Api/Wishlist.Api.csproj src/Services/Wishlist/Wishlist.Api/

RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/Wishlist/Wishlist.Api/Wishlist.Api.csproj

COPY src/Services/Wishlist/ src/Services/Wishlist/
RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,readonly \
    dotnet publish src/Services/Wishlist/Wishlist.Api/Wishlist.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wishlist.Api.dll"]
