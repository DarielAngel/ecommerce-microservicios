# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Reviews/Reviews.Domain/Reviews.Domain.csproj src/Services/Reviews/Reviews.Domain/
COPY src/Services/Reviews/Reviews.Application/Reviews.Application.csproj src/Services/Reviews/Reviews.Application/
COPY src/Services/Reviews/Reviews.Infrastructure/Reviews.Infrastructure.csproj src/Services/Reviews/Reviews.Infrastructure/
COPY src/Services/Reviews/Reviews.Api/Reviews.Api.csproj src/Services/Reviews/Reviews.Api/

RUN dotnet restore src/Services/Reviews/Reviews.Api/Reviews.Api.csproj

COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Reviews/ src/Services/Reviews/
RUN dotnet publish src/Services/Reviews/Reviews.Api/Reviews.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Reviews.Api.dll"]
