# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Services/Promotions/Promotions.Domain/Promotions.Domain.csproj src/Services/Promotions/Promotions.Domain/
COPY src/Services/Promotions/Promotions.Application/Promotions.Application.csproj src/Services/Promotions/Promotions.Application/
COPY src/Services/Promotions/Promotions.Infrastructure/Promotions.Infrastructure.csproj src/Services/Promotions/Promotions.Infrastructure/
COPY src/Services/Promotions/Promotions.Api/Promotions.Api.csproj src/Services/Promotions/Promotions.Api/

RUN dotnet restore src/Services/Promotions/Promotions.Api/Promotions.Api.csproj

COPY src/Services/Promotions/ src/Services/Promotions/
RUN dotnet publish src/Services/Promotions/Promotions.Api/Promotions.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Promotions.Api.dll"]
