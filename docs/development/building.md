# Building and running

See [the overview](README.md) for the other development pages.

## Building

```bash
dotnet build
```

Warnings are errors. The SDK in the Docker image can be newer than the one on a workstation and bring
analyzer rules the local one does not have, so the reference build is the image:

```bash
docker compose build
```

The image is built from the repository root with `docker/Dockerfile` in two stages, the .NET SDK and the
ASP.NET runtime, and runs as the unprivileged user `app`. Its entry point is the server itself, which
also answers two commands instead of serving:

```bash
docker compose run --rm api hash-password   # asks for a password, prints an Argon2id hash for users.yaml
docker compose exec api dotnet OpenVpnPilot.Server.dll healthcheck [url]
```

`healthcheck` asks `/health/live` of the container itself, following `OVP_TLS_MODE`, and exits 0 or 1.
It is what the image's and the Compose file's health check run, because the runtime image has no curl.

## Running it with a test certificate

```bash
mkdir -p certs
openssl req -x509 -newkey rsa:2048 -nodes -days 30 -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost" -keyout certs/server.key -out certs/server.crt
cp .env.example .env    # fill in OVP_DB_PASSWORD, OVP_DATA_KEY and OVP_JWT_SIGNING_KEY
cp config/users.example.yaml config/users.yaml
docker compose up -d --build
```

Swagger is at `https://localhost:8443/swagger`. It fills in the `X-Pilot-*` headers by itself; sign in
through `POST /api/v1/auth/login` and paste the access token into **Authorize**. Swagger is generated
from the XML documentation comments on the controllers and contracts, which is why every action and every
contract has one.

When port 8443 is not available on the host, set `OVP_PUBLISH_PORT`; on Windows hosts Hyper-V can
reserve it. The container port stays 8443.

## Changing the schema

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/OpenVpnPilot.Server --output-dir Data/Migrations
```

Migrations are applied at start, unless `OVP_DB_MIGRATE_ON_START=false`. They are generated code and
exempt from the file length rule. On an empty database EF Core logs one error line while it looks for
its history table before creating it; that line on a first start is expected.

A schema change a deployer would notice gets an entry in `CHANGELOG.md`; deployers are told to back up
the database before updating, see [deployment](../deployment.md#backups).
