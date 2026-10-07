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

COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Reviews/Reviews.Domain/Reviews.Domain.csproj src/Services/Reviews/Reviews.Domain/
COPY src/Services/Reviews/Reviews.Application/Reviews.Application.csproj src/Services/Reviews/Reviews.Application/
COPY src/Services/Reviews/Reviews.Infrastructure/Reviews.Infrastructure.csproj src/Services/Reviews/Reviews.Infrastructure/
COPY src/Services/Reviews/Reviews.Api/Reviews.Api.csproj src/Services/Reviews/Reviews.Api/

RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/Reviews/Reviews.Api/Reviews.Api.csproj

COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Reviews/ src/Services/Reviews/
RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,readonly \
    dotnet publish src/Services/Reviews/Reviews.Api/Reviews.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Reviews.Api.dll"]
