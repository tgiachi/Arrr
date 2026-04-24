# ── Stage 1: Build UI ──────────────────────────────────────────────────────────
FROM node:22-alpine AS ui-builder

WORKDIR /repo
COPY ui/package.json ui/package-lock.json ui/
RUN cd ui && npm ci --prefer-offline

COPY ui/ ui/
# Vite outputs to ../src/Arrr.Service/wwwroot relative to /repo/ui
RUN mkdir -p src/Arrr.Service/wwwroot && cd ui && npm run build


# ── Stage 2: Build .NET service ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS app-builder

WORKDIR /repo

# Pack Arrr.Core into the local feed so plugin projects resolve without a
# published NuGet release (mirrors the scripts/pack-dev.sh workflow).
COPY nuget.config .
COPY src/Arrr.Core/ src/Arrr.Core/
RUN mkdir -p local-packages && \
    dotnet pack src/Arrr.Core/Arrr.Core.csproj -c Release -o local-packages

# Now build the service (wwwroot from Stage 1 lands alongside the binary).
COPY src/Arrr.Service/ src/Arrr.Service/
COPY --from=ui-builder /repo/src/Arrr.Service/wwwroot src/Arrr.Service/wwwroot

RUN dotnet publish src/Arrr.Service/Arrr.Service.csproj \
        -c Release \
        -o /publish


# ── Stage 3: Runtime ───────────────────────────────────────────────────────────
# runtime-deps is the right base for self-contained .NET apps:
# it provides libc / OpenSSL / ICU without the managed runtime.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-bookworm-slim

LABEL org.opencontainers.image.title="Arrr"
LABEL org.opencontainers.image.description="Linux desktop notification aggregator"
LABEL org.opencontainers.image.url="https://github.com/tgiachi/Arrr"
LABEL org.opencontainers.image.licenses="MIT"

# Non-root user
RUN groupadd -r arrr && useradd -r -g arrr -d /data -s /sbin/nologin arrr
RUN mkdir -p /data/plugins /data/logs /data/configs && chown -R arrr:arrr /data

# App lives in /app so the content root (and thus wwwroot lookup) is /app
WORKDIR /app
COPY --from=app-builder /publish/ .
RUN chmod +x Arrr.Service && chown -R arrr:arrr /app

# HTTP API port
EXPOSE 5150

# /data holds arrr.config, plugins/, logs/, configs/
VOLUME ["/data"]

USER arrr

# DBus notifications are unavailable inside Docker; the HTTP API and Unix socket
# still work normally. Pass --logToFile false to keep logs on stdout.
ENTRYPOINT ["./Arrr.Service", "--rootDirectory", "/data", "--logToFile", "false"]
