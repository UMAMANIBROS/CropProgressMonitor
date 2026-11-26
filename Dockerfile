# Multi-stage Dockerfile for CropProgress Monitor (ASP.NET Core 8.0) - Render Ready
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY CropProgressMonitor/CropProgressMonitor.csproj CropProgressMonitor/
RUN dotnet restore CropProgressMonitor/CropProgressMonitor.csproj

# Copy remaining source and publish
COPY CropProgressMonitor/ CropProgressMonitor/
WORKDIR /src/CropProgressMonitor
RUN dotnet publish CropProgressMonitor.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render dynamically binds PORT; default to 8080 locally
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet CropProgressMonitor.dll"]
