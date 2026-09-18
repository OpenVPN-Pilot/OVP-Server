# Built from the repository root: docker build -f docker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restoring from the project file alone keeps the package layer cached while only code changes.
COPY Directory.Build.props .editorconfig ./
COPY src/OpenVpnPilot.Server/OpenVpnPilot.Server.csproj src/OpenVpnPilot.Server/
RUN dotnet restore src/OpenVpnPilot.Server/OpenVpnPilot.Server.csproj

COPY src/ src/
RUN dotnet publish src/OpenVpnPilot.Server/OpenVpnPilot.Server.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The image's own unprivileged user. It needs to write logs and nothing else.
RUN mkdir -p /app/logs /app/config /app/certs && chown app:app /app/logs
USER app

# The base image listens on 8080 unless told otherwise; the server chooses its own listener.
ENV ASPNETCORE_HTTP_PORTS= \
    OVP_LOG_DIRECTORY=/app/logs \
    OVP_AUTH_FILE=/app/config/users.yaml \
    DOTNET_NOLOGO=1

EXPOSE 8443 8080
HEALTHCHECK --interval=30s --timeout=8s --start-period=40s --retries=3 \
    CMD ["dotnet", "OpenVpnPilot.Server.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "OpenVpnPilot.Server.dll"]
