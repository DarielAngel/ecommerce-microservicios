# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Users/Users.Domain/Users.Domain.csproj src/Services/Users/Users.Domain/
COPY src/Services/Users/Users.Application/Users.Application.csproj src/Services/Users/Users.Application/
COPY src/Services/Users/Users.Infrastructure/Users.Infrastructure.csproj src/Services/Users/Users.Infrastructure/
COPY src/Services/Users/Users.Api/Users.Api.csproj src/Services/Users/Users.Api/

RUN dotnet restore src/Services/Users/Users.Api/Users.Api.csproj

COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Users/ src/Services/Users/
RUN dotnet publish src/Services/Users/Users.Api/Users.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Users.Api.dll"]
