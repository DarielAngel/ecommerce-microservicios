# Contexto de build esperado: raíz del repositorio (necesario para alcanzar src/Shared/Contracts).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Inventory/Inventory.Domain/Inventory.Domain.csproj src/Services/Inventory/Inventory.Domain/
COPY src/Services/Inventory/Inventory.Application/Inventory.Application.csproj src/Services/Inventory/Inventory.Application/
COPY src/Services/Inventory/Inventory.Infrastructure/Inventory.Infrastructure.csproj src/Services/Inventory/Inventory.Infrastructure/
COPY src/Services/Inventory/Inventory.Api/Inventory.Api.csproj src/Services/Inventory/Inventory.Api/

RUN dotnet restore src/Services/Inventory/Inventory.Api/Inventory.Api.csproj

COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Inventory/ src/Services/Inventory/
RUN dotnet publish src/Services/Inventory/Inventory.Api/Inventory.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
