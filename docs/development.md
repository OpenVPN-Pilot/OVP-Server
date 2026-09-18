# Working on OpenVPN Pilot Server

## Architecture

One ASP.NET Core project on .NET 10, in three layers.

```
src/OpenVpnPilot.Server/
  Controllers/     bind the request, state who may call it, return what the service returns
  Services/        every decision: validation, permissions beyond the role, encryption, change numbers
  Repositories/    Entity Framework and nothing else
  Data/            DbContext, entities, their configuration, migrations
  Contracts/       the request and response records, the error codes and header names
  Mapping/         entity to contract, by hand
  Auth/            the four identity providers, token issuing, the current user
  Middleware/      request id, errors, HTTPS, mandatory headers, account revocation
  Security/        AES-GCM for data at rest, Argon2id for the user file, certificates
  Configuration/   the environment, read once into records and checked at start
  Logging/         Serilog, hourly files, retention
  Background/      hourly maintenance
  OpenApi/         Swagger
  Extensions/      service registration and the request pipeline
  Commands/        hash-password and the container health probe
```

Things worth knowing before changing anything:

- **Order of the pipeline** is in `Extensions/PipelineSetup.cs` and is the design: request id first,
  errors next, transport and headers before authentication, the account check after it.
- **Refusals are exceptions.** A service throws `ServiceException` with a status and a code from
  `Contracts/ErrorCodes.cs`; the middleware turns it into problem details. Controllers contain no
  error handling.
- **Synchronised writes** go through `IUnitOfWork.BeginSyncedWriteAsync`, which takes a PostgreSQL
  advisory lock and a number from the `change_seq` sequence. Anything a client synchronises carries
  that number, and a deletion leaves a tombstone with it. A write that bypasses this is invisible to
  clients.
- **Secrets at rest** go through `ISecretCipher` with a context naming the row, so a value copied to
  another row does not decrypt.
- **Log messages** are source generated with `[LoggerMessage]` in a `*Log.cs` class next to the code,
  like the client. Event ids by area are listed in `Logging/HostLog.cs`.

## Building

```bash
dotnet build
```

Warnings are errors. The SDK in the Docker image can be newer than the one on a workstation and bring
analyzer rules the local one does not have, so the reference build is the image:

```bash
docker compose build
```

Running locally against the Compose database, with a test certificate:

```bash
mkdir -p certs
openssl req -x509 -newkey rsa:2048 -nodes -days 30 -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost" -keyout certs/server.key -out certs/server.crt
cp .env.example .env    # fill in the three secrets
cp config/users.example.yaml config/users.yaml
docker compose up -d --build
```

Swagger is at `https://localhost:8443/swagger`. It fills in the `X-Pilot-*` headers by itself; sign in
through `POST /api/v1/auth/login` and paste the access token into **Authorize**.

## Changing the schema

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/OpenVpnPilot.Server --output-dir Data/Migrations
```

Migrations are applied at start. They are generated code and exempt from the file length rule.

## Testing

There are no unit tests, on purpose; the reasons are in `CLAUDE.md`. A change is proven against the
running stack. `none` and `file` need nothing more than Swagger or `curl`. For `ldap` and `entra`,
`lab/` adds two stand-in identity providers to the same Compose project:

```bash
docker compose -f docker-compose.yaml -f lab/docker-compose.yaml up -d --build
```

**`ad`** is a Samba 4 domain controller for `CORP.EXAMPLE.COM`, provisioned on first start, which answers
LDAP the way Windows Server does. Its users and groups:

| User | Password | Groups |
| --- | --- | --- |
| `anna` | `Anna-Pass-2026!` | OVP Admins, OVP Users |
| `ben` | `Ben-Pass-2026!` | Team A, which is a member of OVP Users |
| `clara` | `Clara-Pass-2026!` | none |
| `svc-ovp` | `Svc-Pass-2026!` | the service account |

```
OVP_AUTH_MODE=ldap
OVP_LDAP_HOST=dc1.corp.example.com
OVP_LDAP_CA_CERT_PATH=/lab/trust/ad-ca.pem
OVP_LDAP_BIND_DN=CN=svc-ovp,CN=Users,DC=corp,DC=example,DC=com
OVP_LDAP_BIND_PASSWORD=Svc-Pass-2026!
OVP_LDAP_BASE_DN=DC=corp,DC=example,DC=com
OVP_LDAP_ADMIN_GROUP=CN=OVP Admins,CN=Users,DC=corp,DC=example,DC=com
OVP_LDAP_USER_GROUP=CN=OVP Users,CN=Users,DC=corp,DC=example,DC=com
```

Change the directory with `docker compose exec ad samba-tool ...`, for example `user disable ben` or
`group removemembers "OVP Users" "Team A"`, and watch the next refresh.

**`entra-mock`** publishes discovery and signing keys at Entra's paths and mints tokens shaped like
Entra's with whatever claims a test needs. It creates its own certificate authority at start and puts
it into the lab's trust folder, which the API trusts in the lab; restart the API after restarting the
mock.

```
OVP_AUTH_MODE=entra
OVP_ENTRA_INSTANCE=https://entra-mock:9443
OVP_ENTRA_TENANT_ID=aaaaaaaa-0000-0000-0000-000000000001
OVP_ENTRA_CLIENT_ID=bbbbbbbb-0000-0000-0000-000000000002
```

```bash
docker compose -f docker-compose.yaml -f lab/docker-compose.yaml exec entra-mock curl -sk \
  "https://localhost:9443/mint?tenant=aaaaaaaa-0000-0000-0000-000000000001&aud=api://bbbbbbbb-0000-0000-0000-000000000002&roles=Admin&upn=admin@example.com"
```

The token goes to `POST /api/v1/auth/entra/exchange`. Pass the same `oid` every time a test signs in
as the same person, as Entra would: without it the mock invents one, and the server then refuses the
known name as belonging to someone else. Pass `azp` with the client id, as Entra does. `/mint` also
takes `scp`, `groups`,
`name`, `exp` in minutes (negative for an expired token), `ver=1` for a version 1 token, `noscp=1`,
`rogue=1` for a signature by a key the mock does not publish, and `iss` to claim another issuer.

The lab is for tests only: fixed passwords, a privileged container, authorities that live for a month.

For every change to behaviour a client sees, check the error code and the log line as well as the
happy path, and update `docs/client-integration.md`.

## Contributing

Issues and pull requests are welcome. Work happens on `dev`; `master` only receives releases.

- **Say why in the code.** Comments explain the reason, never the mechanism. The house rules are in
  `CLAUDE.md`, and it is the standard the code is held to.
- **Keep files short.** Around 200 lines, never more than 300, generated migrations aside.
- **Keep the contract.** An endpoint, header, error code or behaviour a client depends on changes
  `docs/client-integration.md` in the same commit; an environment variable changes `.env.example` and
  `configuration.md`; anything a deployer or client developer notices gets a line in `CHANGELOG.md`.
- Commits are English, short, imperative and start with a gitmoji code, for example
  `:sparkles: add vault endpoints`.

## How this was built

The code was written with Claude Opus 5 working from the client's rules and code. `CLAUDE.md` is not
a prompt; it is the standard the code is held to. Judge it by the code.
