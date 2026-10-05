# Deploying OpenVPN Pilot Server

The server runs as two containers from one Compose file: the API and PostgreSQL. Everything it needs
from the outside is a `.env` file, a TLS certificate, and in mode `file` a user list.

## Requirements

- Docker Engine 24 or newer with the Compose plugin, on Linux, or Docker Desktop.
- A host name clients reach the server by, and a certificate for it. HTTPS is mandatory: the API
  carries passwords and private keys inside TLS and refuses anything that arrives without it.
- Outbound HTTPS to `login.microsoftonline.com` in mode `entra`, and a route to the directory in mode
  `ldap`. Nothing else leaves the server.

## First start

```bash
git clone https://github.com/OpenVPN-Pilot/OVP-Server.git
cd OVP-Server
cp .env.example .env
```

Fill in the three secrets `.env` requires:

```bash
openssl rand -base64 32   # OVP_DATA_KEY
openssl rand -base64 48   # OVP_JWT_SIGNING_KEY
openssl rand -hex 24      # OVP_DB_PASSWORD
```

`.env` holds secrets: `chmod 600 .env`. Better still, give the keys and passwords as Docker secrets
through `<NAME>_FILE`, see [configuration.md](configuration.md).

**Keep a copy of `OVP_DATA_KEY` somewhere other than this machine.** Every profile configuration and
every vault entry is encrypted with it. A database backup without the key is unreadable, by design.

Then choose how TLS is terminated and how people sign in, described below and in
[authentication.md](authentication.md), and start it:

```bash
docker compose up -d --build
docker compose logs -f api
```

The first start creates the schema. `GET https://<host>:8443/api/v1/server/info` answers once it is up.

## TLS

### The server terminates TLS (`OVP_TLS_MODE=kestrel`)

Put the certificate into `./certs`, which is mounted read only at `/app/certs`:

- PEM: `server.crt` (with the chain) and `server.key`, named by `OVP_TLS_CERT_PATH` and
  `OVP_TLS_KEY_PATH`. An encrypted key takes its passphrase from `OVP_TLS_CERT_PASSWORD`.
- PKCS#12: a `.pfx` or `.p12` in `OVP_TLS_CERT_PATH`, with `OVP_TLS_CERT_PASSWORD`.

Only an HTTPS listener exists, on `OVP_HTTPS_PORT` (8443), published on the host as `OVP_PUBLISH_PORT`.
A renewed certificate is picked up by restarting the API container. The log warns at every start when
the certificate expires within 30 days.

### A reverse proxy terminates TLS (`OVP_TLS_MODE=proxy`)

The API listens for plain HTTP on `OVP_HTTP_PORT` (8080) and believes `X-Forwarded-Proto` only from the
addresses in `OVP_TRUSTED_PROXIES`. A request that the proxy did not receive over HTTPS, or that comes
from anywhere else, is refused with `transport.https_required`. Set `OVP_CONTAINER_PORT=8080`, or
better, remove the `ports` section and put the proxy on the Compose network.

The proxy must pass `X-Forwarded-For` and `X-Forwarded-Proto` and must not buffer or rewrite the
`X-Pilot-*` headers. With Caddy on the same network:

```
vpn-api.example.com {
    reverse_proxy api:8080
}
```

and `OVP_TRUSTED_PROXIES` set to the Compose network, for example `172.16.0.0/12`.

## Where things are kept

| What | Where |
| --- | --- |
| Database | `./data/db` |
| Logs | `./data/api/logs/yyyy-MM-dd/HH.log`, see [operations.md](operations.md) |
| Certificates | `./certs`, read only |
| User list for mode `file` | `./config/users.yaml`, read only |
| Configuration | `.env` |

The API container runs as the unprivileged user `app` (uid 1654) and must be able to write `./data/api/logs`.
On Linux, `sudo mkdir -p data/api/logs data/db && sudo chown 1654:1654 data/api/logs` once. PostgreSQL
sets up `./data/db` itself.

Everything the server keeps lives under `./data`, the database apart from the API. The folder is ignored
by git and by the image build. Copy it only with the stack stopped, or use the backup below.

## Backups

The database holds everything; the containers hold nothing worth keeping.

```bash
docker compose exec -T postgres pg_dump -U ovp -Fc ovp > ovp-$(date +%F).dump
```

Restore into a fresh stack with `pg_restore --clean`. A backup is only useful together with the
`OVP_DATA_KEY` it was written under. Clients that synchronised after the backup was taken start over
with a full synchronisation on their own.

## Updating

```bash
git pull
docker compose up -d --build
```

Schema migrations run at start unless `OVP_DB_MIGRATE_ON_START=false`. Read [CHANGELOG.md](../CHANGELOG.md)
before a new major version.
