# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Notifications/Notifications.Domain/Notifications.Domain.csproj src/Services/Notifications/Notifications.Domain/
COPY src/Services/Notifications/Notifications.Application/Notifications.Application.csproj src/Services/Notifications/Notifications.Application/
COPY src/Services/Notifications/Notifications.Infrastructure/Notifications.Infrastructure.csproj src/Services/Notifications/Notifications.Infrastructure/
COPY src/Services/Notifications/Notifications.Api/Notifications.Api.csproj src/Services/Notifications/Notifications.Api/

RUN dotnet restore src/Services/Notifications/Notifications.Api/Notifications.Api.csproj

COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Notifications/ src/Services/Notifications/
RUN dotnet publish src/Services/Notifications/Notifications.Api/Notifications.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Notifications.Api.dll"]
