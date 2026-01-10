# =========================================================
# Base runtime (Production)
# =========================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# ---------------------------------------------------------
# Chromium runtime dependencies (MANDATORY)
# ---------------------------------------------------------
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    fonts-liberation \
    libasound2t64 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libcups2 \
    libdrm2 \
    libgbm1 \
    libgtk-3-0 \
    libnss3 \
    libx11-xcb1 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxkbcommon0 \
    libxrandr2 \
    libxshmfence1 \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Playwright runtime config
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1

# AWS env passthrough (runtime only)
ENV AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}
ENV AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}
ENV AWS_REGION=${AWS_REGION:-ap-south-1}
ENV AWS_DEFAULT_REGION=${AWS_DEFAULT_REGION:-ap-south-1}


# =========================================================
# Build stage (SDK + Playwright install)
# =========================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# IMPORTANT: Playwright browser path MUST exist
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN mkdir -p /ms-playwright

# Copy sources
COPY backend backend/
COPY shared shared/

WORKDIR /src/backend/backend

# ---------------------------------------------------------
# Restore + Build (DEFAULT output — REQUIRED)
# ---------------------------------------------------------
RUN dotnet restore
RUN dotnet build backend.csproj -c $BUILD_CONFIGURATION

# ---------------------------------------------------------
# Install Playwright CLI (SDK ONLY)
# ---------------------------------------------------------
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="/root/.dotnet/tools:${PATH}"

# ---------------------------------------------------------
# Install Chromium (NOW WORKS)
# ---------------------------------------------------------
RUN playwright install chromium

# ---------------------------------------------------------
# Publish app
# ---------------------------------------------------------
RUN dotnet publish backend.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false


# =========================================================
# Final runtime image (NO SDK)
# =========================================================
FROM base AS final
WORKDIR /app

# Copy Playwright browsers
COPY --from=build /ms-playwright /ms-playwright

# Copy published app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "backend.dll"]
