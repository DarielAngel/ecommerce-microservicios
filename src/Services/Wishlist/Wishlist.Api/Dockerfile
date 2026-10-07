# Contexto de build esperado: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Services/Wishlist/Wishlist.Domain/Wishlist.Domain.csproj src/Services/Wishlist/Wishlist.Domain/
COPY src/Services/Wishlist/Wishlist.Application/Wishlist.Application.csproj src/Services/Wishlist/Wishlist.Application/
COPY src/Services/Wishlist/Wishlist.Infrastructure/Wishlist.Infrastructure.csproj src/Services/Wishlist/Wishlist.Infrastructure/
COPY src/Services/Wishlist/Wishlist.Api/Wishlist.Api.csproj src/Services/Wishlist/Wishlist.Api/

RUN dotnet restore src/Services/Wishlist/Wishlist.Api/Wishlist.Api.csproj

COPY src/Services/Wishlist/ src/Services/Wishlist/
RUN dotnet publish src/Services/Wishlist/Wishlist.Api/Wishlist.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wishlist.Api.dll"]
