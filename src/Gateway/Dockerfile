# Contexto de build esperado: raíz del repositorio (para poder alcanzar src/Shared si algún
# día el Gateway también lo necesita; por ahora no lo usa, pero mantenemos el mismo patrón
# que el resto de los servicios por consistencia).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Gateway/Gateway.csproj src/Gateway/

RUN dotnet restore src/Gateway/Gateway.csproj

COPY src/Gateway/ src/Gateway/
RUN dotnet publish src/Gateway/Gateway.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gateway.dll"]
