# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Los paquetes NuGet se guardan en una caché de BuildKit compartida por todos los servicios:
# se descargan una sola vez y sobreviven aunque cambie la imagen base o se reconstruya todo.
# "locked" hace que restore y publish usen la caché de a un servicio a la vez: así nadie
# lee paquetes a medio escribir (con builds en paralelo, "readonly" veía la caché incompleta).
# Reintentos ante cortes de red/DNS al bajar de nuget.org:
ENV NUGET_ENABLE_ENHANCED_HTTP_RETRY=true \
    NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT=8 \
    NUGET_ENHANCED_NETWORK_RETRY_DELAY_MILLISECONDS=3000

COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Promotions/Promotions.Domain/Promotions.Domain.csproj src/Services/Promotions/Promotions.Domain/
COPY src/Services/Promotions/Promotions.Application/Promotions.Application.csproj src/Services/Promotions/Promotions.Application/
COPY src/Services/Promotions/Promotions.Infrastructure/Promotions.Infrastructure.csproj src/Services/Promotions/Promotions.Infrastructure/
COPY src/Services/Promotions/Promotions.Api/Promotions.Api.csproj src/Services/Promotions/Promotions.Api/

RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/Promotions/Promotions.Api/Promotions.Api.csproj

COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Promotions/ src/Services/Promotions/
RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Services/Promotions/Promotions.Api/Promotions.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Promotions.Api.dll"]
