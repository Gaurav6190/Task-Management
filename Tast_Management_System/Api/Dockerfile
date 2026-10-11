# syntax=docker/dockerfile:1.7
# TaskManagement.Api - multi-stage build. Build context: ./Api
#   target "final"    -> the Web API runtime image (default)
#   target "migrator" -> one-shot EF Core migration bundle that upgrades the database

ARG DOTNET_VERSION=8.0

# ---------- restore (cached until a .csproj / build prop changes) ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY .config/dotnet-tools.json .config/
COPY src/TaskManagement.Domain/TaskManagement.Domain.csproj                 src/TaskManagement.Domain/
COPY src/TaskManagement.Application/TaskManagement.Application.csproj       src/TaskManagement.Application/
COPY src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj src/TaskManagement.Infrastructure/
COPY src/TaskManagement.Api/TaskManagement.Api.csproj                       src/TaskManagement.Api/
RUN dotnet restore src/TaskManagement.Api/TaskManagement.Api.csproj \
 && dotnet tool restore

# ---------- build & publish ----------
FROM restore AS publish
ARG BUILD_CONFIGURATION=Release
COPY src/ src/
RUN dotnet publish src/TaskManagement.Api/TaskManagement.Api.csproj \
        -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

# ---------- EF Core migrations bundle ----------
FROM publish AS bundle
RUN dotnet ef migrations bundle \
        --project src/TaskManagement.Infrastructure \
        --startup-project src/TaskManagement.Api \
        --configuration Release \
        --output /app/efbundle --force

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
USER $APP_UID
# Connection string comes from ConnectionStrings__Default (read by the API's Program.cs).
ENTRYPOINT ["./efbundle"]

# ---------- runtime ----------
# Chiseled = distroless Ubuntu: no shell/package manager, non-root by default.
# "-extra" keeps ICU + tzdata, which Microsoft.Data.SqlClient needs.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=publish /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskManagement.Api.dll"]
