# syntax=docker/dockerfile:1
# One production image: the Angular build served by the API (D008, D032). Images are pinned by tag and digest,
# each at least two weeks old when chosen (DECISIONS.md D032). Build: docker compose up --build

# 1. The SPA: production build plus precompressed .br/.gz copies (npm run build).
FROM node:24.20.0-bookworm-slim@sha256:ba849c60be29959425b8734d57b8b4b7d56f98edd9504c9af091d5281095a71e AS web
WORKDIR /src/web
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/web/ ./
RUN npm run build

# 2. The API, published for Linux x64 (framework-dependent; the runtime image provides .NET).
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/api/FitnessMerchant.Api/FitnessMerchant.Api.csproj src/api/FitnessMerchant.Api/
RUN dotnet restore src/api/FitnessMerchant.Api/FitnessMerchant.Api.csproj --runtime linux-x64
COPY src/api/ src/api/
RUN dotnet publish src/api/FitnessMerchant.Api/FitnessMerchant.Api.csproj --configuration Release --runtime linux-x64 \
    --self-contained false --no-restore --output /app

# 3. Runtime: chiseled Ubuntu (no shell, no package manager), with ICU and time zones ("extra"). Non-root by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled-extra@sha256:00e0ad6a7ef8c0c1391b87f05c7ac757a15740455688f2bfcd146a3f4b987efd
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /src/web/dist/web/browser ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Spa__RootPath=wwwroot
# The chiseled images define the non-root "app" user (APP_UID 1654); say so explicitly.
USER $APP_UID
EXPOSE 8080
# No curl in a chiseled image: the app checks itself (Features/Health/HealthProbe.cs).
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 CMD ["dotnet", "FitnessMerchant.Api.dll", "--health-check"]
ENTRYPOINT ["dotnet", "FitnessMerchant.Api.dll"]
