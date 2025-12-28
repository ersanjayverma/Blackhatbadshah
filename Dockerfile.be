# See https://aka.ms/customizecontainer to learn how to customize your debug container
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# =========================================================
# Base runtime (VS Fast Mode / Production Runtime)
# =========================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# Install wkhtmltopdf IN RUNTIME IMAGE
RUN apt-get update && apt-get install -y --no-install-recommends \
    wkhtmltopdf \
    fontconfig \
    libfreetype6 \
    libjpeg-turbo8 \
    libpng16-16 \
    libx11-6 \
    libxcb1 \
    libxext6 \
    libxrender1 \
    xfonts-75dpi \
    xfonts-base \
    && rm -rf /var/lib/apt/lists/*

# -----------------------------
# AWS credentials (from environment)
# -----------------------------
# Pass these at runtime via docker run -e or docker-compose
ENV AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}
ENV AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}
ENV AWS_REGION=${AWS_REGION:-ap-south-1}
ENV AWS_DEFAULT_REGION=${AWS_DEFAULT_REGION:-ap-south-1}

# =========================================================
# Build stage
# =========================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY backend backend/
COPY shared shared/

WORKDIR /src/backend/backend
RUN dotnet restore
RUN dotnet build "./backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

# =========================================================
# Publish stage
# =========================================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./backend.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# =========================================================
# Final runtime image
# =========================================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "backend.dll"]
