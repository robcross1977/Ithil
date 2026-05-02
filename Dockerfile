# syntax=docker/dockerfile:1

# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies first so this layer is cached on code-only changes.
COPY ["src/Ithil.Gateway/Ithil.Gateway.csproj",         "src/Ithil.Gateway/"]
COPY ["src/Ithil.Core/Ithil.Core.csproj",               "src/Ithil.Core/"]
COPY ["src/Ithil.Budget/Ithil.Budget.csproj",           "src/Ithil.Budget/"]
COPY ["src/Ithil.Cache/Ithil.Cache.csproj",             "src/Ithil.Cache/"]
COPY ["src/Ithil.Privacy/Ithil.Privacy.csproj",         "src/Ithil.Privacy/"]
COPY ["src/Ithil.Management/Ithil.Management.csproj",   "src/Ithil.Management/"]
COPY ["src/Ithil.Dashboard/Ithil.Dashboard.csproj",     "src/Ithil.Dashboard/"]
RUN dotnet restore "src/Ithil.Gateway/Ithil.Gateway.csproj"

# Copy source and publish.
COPY src/ src/
RUN dotnet publish "src/Ithil.Gateway/Ithil.Gateway.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Download the ONNX embedding model and vocabulary file from HuggingFace.
# These are gitignored binaries (~23 MB) baked into the image at build time.
# To use a pre-downloaded model instead, mount it over /app/models at runtime.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && BASE="https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main" \
    && mkdir -p /app/publish/models \
    && curl -fL "$BASE/onnx/model.onnx" -o /app/publish/models/all-MiniLM-L6-v2.onnx \
    && curl -fL "$BASE/vocab.txt"        -o /app/publish/models/vocab.txt \
    && apt-get remove -y curl && rm -rf /var/lib/apt/lists/*

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as a non-root user — required by most Kubernetes security policies.
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Ithil.Gateway.dll"]
