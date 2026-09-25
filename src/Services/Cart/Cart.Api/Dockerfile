# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Services/Cart/Cart.Domain/Cart.Domain.csproj src/Services/Cart/Cart.Domain/
COPY src/Services/Cart/Cart.Application/Cart.Application.csproj src/Services/Cart/Cart.Application/
COPY src/Services/Cart/Cart.Infrastructure/Cart.Infrastructure.csproj src/Services/Cart/Cart.Infrastructure/
COPY src/Services/Cart/Cart.Api/Cart.Api.csproj src/Services/Cart/Cart.Api/

RUN dotnet restore src/Services/Cart/Cart.Api/Cart.Api.csproj

COPY src/Services/Cart/ src/Services/Cart/
RUN dotnet publish src/Services/Cart/Cart.Api/Cart.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cart.Api.dll"]
