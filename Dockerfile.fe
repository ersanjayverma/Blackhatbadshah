# Use Alpine as the base image
FROM alpine:latest

# Install dependencies, .NET 10 SDK, and Supervisor
# Note: dotnet10-sdk is available in Alpine 'edge' community repo as of late 2025
RUN apk add --no-cache \
    supervisor \
    icu-libs \
    krb5-libs \
    libgcc \
    libintl \
    libssl3 \
    libstdc++ \
    zlib \
    bash \
    --repository=dl-cdn.alpinelinux.org \
    dotnet10-sdk

# Set working directory
WORKDIR /app


# Copy the rest of the source code
COPY frontend frontend/
COPY shared shared/
WORKDIR /app/frontend/frontend
RUN dotnet restore
# Copy Supervisor configuration
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Set environment variable for Release mode
ENV DOTNET_CONFIGURATION=Release
# Run Supervisor
CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
