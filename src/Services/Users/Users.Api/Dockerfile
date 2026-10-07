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
COPY src/Services/Users/Users.Domain/Users.Domain.csproj src/Services/Users/Users.Domain/
COPY src/Services/Users/Users.Application/Users.Application.csproj src/Services/Users/Users.Application/
COPY src/Services/Users/Users.Infrastructure/Users.Infrastructure.csproj src/Services/Users/Users.Infrastructure/
COPY src/Services/Users/Users.Api/Users.Api.csproj src/Services/Users/Users.Api/

RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/Users/Users.Api/Users.Api.csproj

COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Users/ src/Services/Users/
RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,readonly \
    dotnet publish src/Services/Users/Users.Api/Users.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Users.Api.dll"]
