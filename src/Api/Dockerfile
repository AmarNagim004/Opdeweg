# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Domain/Opdeweg.Domain.csproj src/Domain/
COPY src/Application/Opdeweg.Application.csproj src/Application/
COPY src/Infrastructure/Opdeweg.Infrastructure.csproj src/Infrastructure/
COPY src/Api/Opdeweg.Api.csproj src/Api/
RUN dotnet restore src/Api/Opdeweg.Api.csproj
COPY src/Domain src/Domain
COPY src/Application src/Application
COPY src/Infrastructure src/Infrastructure
COPY src/Api src/Api
RUN dotnet publish src/Api/Opdeweg.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
# Probe GET /health/live (liveness) and /health/ready (PostgreSQL + Redis) from your orchestrator.
ENTRYPOINT ["dotnet", "Opdeweg.Api.dll"]
