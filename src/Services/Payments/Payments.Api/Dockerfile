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

COPY src/Services/Payments/Payments.Domain/Payments.Domain.csproj src/Services/Payments/Payments.Domain/
COPY src/Services/Payments/Payments.Application/Payments.Application.csproj src/Services/Payments/Payments.Application/
COPY src/Services/Payments/Payments.Infrastructure/Payments.Infrastructure.csproj src/Services/Payments/Payments.Infrastructure/
COPY src/Services/Payments/Payments.Api/Payments.Api.csproj src/Services/Payments/Payments.Api/

RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/Payments/Payments.Api/Payments.Api.csproj

COPY src/Services/Payments/ src/Services/Payments/
RUN --mount=type=cache,id=nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Services/Payments/Payments.Api/Payments.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Payments.Api.dll"]
