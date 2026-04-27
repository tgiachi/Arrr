# ── Stage 1: Build UI ────────────────────────────────────────────────────────
FROM node:22-alpine AS ui-builder
WORKDIR /build/ui
COPY ui/package*.json ./
RUN npm ci
COPY ui/ ./
RUN npm run build

# ── Stage 2: Publish .NET ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-builder
WORKDIR /build
COPY src/ src/
COPY --from=ui-builder /build/src/Arrr.Service/wwwroot/ src/Arrr.Service/wwwroot/
RUN dotnet publish src/Arrr.Service/Arrr.Service.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o /publish

# ── Stage 3: Runtime image ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=dotnet-builder /publish/ ./
COPY docker-entrypoint.sh ./
RUN mv Arrr.Service arrr \
    && chmod +x arrr docker-entrypoint.sh \
    && rm -f *.pdb arrr.config

EXPOSE 5150
VOLUME ["/data"]
ENV XDG_DATA_HOME=/data

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -sf http://localhost:5150/api/version || exit 1

ENTRYPOINT ["/app/docker-entrypoint.sh"]
