# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Services/Payments/Payments.Domain/Payments.Domain.csproj src/Services/Payments/Payments.Domain/
COPY src/Services/Payments/Payments.Application/Payments.Application.csproj src/Services/Payments/Payments.Application/
COPY src/Services/Payments/Payments.Infrastructure/Payments.Infrastructure.csproj src/Services/Payments/Payments.Infrastructure/
COPY src/Services/Payments/Payments.Api/Payments.Api.csproj src/Services/Payments/Payments.Api/

RUN dotnet restore src/Services/Payments/Payments.Api/Payments.Api.csproj

COPY src/Services/Payments/ src/Services/Payments/
RUN dotnet publish src/Services/Payments/Payments.Api/Payments.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Payments.Api.dll"]
