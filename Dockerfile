FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY RevendaVeiculos.sln ./
COPY src/RevendaVeiculos.Domain/RevendaVeiculos.Domain.csproj src/RevendaVeiculos.Domain/
COPY src/RevendaVeiculos.Application/RevendaVeiculos.Application.csproj src/RevendaVeiculos.Application/
COPY src/RevendaVeiculos.Infrastructure/RevendaVeiculos.Infrastructure.csproj src/RevendaVeiculos.Infrastructure/
COPY src/RevendaVeiculos.Api/RevendaVeiculos.Api.csproj src/RevendaVeiculos.Api/
COPY tests/RevendaVeiculos.UnitTests/RevendaVeiculos.UnitTests.csproj tests/RevendaVeiculos.UnitTests/
COPY tests/RevendaVeiculos.IntegrationTests/RevendaVeiculos.IntegrationTests.csproj tests/RevendaVeiculos.IntegrationTests/

RUN dotnet restore RevendaVeiculos.sln

COPY src/ src/
RUN dotnet publish src/RevendaVeiculos.Api/RevendaVeiculos.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RevendaVeiculos.Api.dll"]
