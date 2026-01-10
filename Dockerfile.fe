# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

COPY frontend frontend/
COPY shared shared/

WORKDIR /app/frontend/frontend
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM nginx:alpine

COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY nginx.conf.frontend /etc/nginx/conf.d/default.conf

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]