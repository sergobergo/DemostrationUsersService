
# ============================================================
# BASE: общий runtime для dev и prod
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Non-root пользователь для prod
RUN groupadd -r appuser && useradd -r -g appuser -u 1001 appuser \
    && mkdir -p /app/logs \
    && chown -R appuser:appuser /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    DOTNET_EnableDiagnostics=0

# ============================================================
# RESTORE: отдельный слой для кэша NuGet
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

# Копируем ТОЛЬКО .csproj для максимального кэша restore
COPY ["UserService.API/UserService.API.csproj",                 "UserService.API/"]
COPY ["UsersService.Application/UsersService.Application.csproj","UsersService.Application/"]
COPY ["UsersService.Domain/UsersService.Domain.csproj",         "UsersService.Domain/"]
COPY ["UserService.UnitTests/UserService.UnitTests.csproj",     "UserService.UnitTests/"]
COPY ["UserService.IntegrationTests/UserService.IntegrationTests.csproj", "UserService.IntegrationTests/"]

# Restore только API (тянет Application + Domain транзитивно)
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "UserService.API/UserService.API.csproj"

# ============================================================
# BUILD: компиляция
# ============================================================
FROM restore AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем весь исходный код
COPY ["UserService.API/",                 "UserService.API/"]
COPY ["UsersService.Application/",        "UsersService.Application/"]
COPY ["UsersService.Domain/",             "UsersService.Domain/"]

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet build "UserService.API/UserService.API.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/build \
        --no-restore

# ============================================================
# PUBLISH: подготовка артефакта для prod
# ============================================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "UserService.API/UserService.API.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ============================================================
# DEVELOPMENT: с SDK, поддержка dotnet watch / hot reload
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
WORKDIR /src
EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    DOTNET_RUNNING_IN_CONTAINER=true

# Копируем код (в compose перекроется volume mount'ом для hot reload)
COPY --from=build /src /src

WORKDIR /src/UserService.API
ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--urls", "http://+:8080"]

# ============================================================
# PRODUCTION: минимальный runtime image
# ============================================================
FROM base AS production
WORKDIR /app

COPY --from=publish --chown=appuser:appuser /app/publish .

USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "UserService.API.dll"]

# ============================================================
# DEFAULT TARGET = production
# ============================================================
FROM production AS final