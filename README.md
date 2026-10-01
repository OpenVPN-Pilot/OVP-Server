# OpenVPN Pilot Server

The server for [OpenVPN Pilot](https://github.com/OpenVPN-Pilot/OVP-Client): one place a team keeps
its OpenVPN profiles, the credentials those profiles need, and each person's favourites, shortcuts and
settings.

Without it every machine imports the same profiles and every person types the same passwords, once per
profile, and again on the next machine. With it an administrator imports a profile once, the first
person to connect shares the sign in, and everyone else simply connects. The client keeps doing the
connecting; the server never starts a tunnel and never sees one.

It is a REST API in one ASP.NET Core project, runs in Docker Compose next to PostgreSQL, and signs
people in with a name only, a YAML user list, LDAP or Active Directory, or Entra ID.

> **Status: version 1.0.0.** Every endpoint is tested against the Compose stack, including
> synchronisation and refresh under concurrent requests. Sign in is proven against OpenLDAP, against a
> Samba 4 Active Directory domain controller, and against a stand-in for Entra ID in
> [`lab/`](lab/docker-compose.yaml); a real Entra ID tenant has not been tried yet. The desktop client
> does not speak to it yet; its remote mode is next. See [Roadmap](#roadmap).

## Documentation

| | |
| --- | --- |
| [Deploying it](docs/deployment.md) | Compose, TLS in the server or behind a proxy, backups, updates |
| [Configuration](docs/configuration.md) | Every environment variable |
| [Signing in](docs/authentication.md) | The four modes, the user file, LDAP and AD, the Entra app registration |
| [Running it](docs/operations.md) | Logs and their retention, health, taking access away, keys |
| [Integrating the client](docs/client-integration.md) | The contract: endpoints, headers, errors, synchronisation, the wipe directive |
| [Working on it](docs/development.md) | Architecture, building, schema changes, testing, contributing |
| [Changelog](CHANGELOG.md) | What changed in each version |

## Features

- Shared profiles and tags: everyone reads and connects, administrators import, change and delete
- Bulk import of up to 500 profiles at once, with duplicates recognised the way the client does
- A shared vault: one sign in per profile and realm; anyone may add a missing one, administrators
  replace and delete
- Favourites, shortcuts and settings that follow each person to every machine
- Synchronisation from a cursor, deletions included, so a client keeps a local copy and works offline
- Four ways of signing in: a name only, a YAML user list, LDAP or Active Directory, Entra ID
- Two roles, taken from the identity provider
- A wipe directive: a client whose account was disabled or removed erases everything it received
- Mandatory client headers: version, installation, platform and timestamp, checked on every call
- HTTPS only, in the server or behind a reverse proxy
- Configurations and vault entries encrypted at rest with AES-GCM in PostgreSQL
- Detailed logs on the console and in hourly files, kept for seven days
- Swagger, switched on and off with one variable

## Quick start

You need Docker with Compose v2 and a TLS certificate for the name clients reach the server by.

```bash
git clone https://github.com/OpenVPN-Pilot/OVP-Server.git && cd OVP-Server
cp .env.example .env                        # fill in OVP_DB_PASSWORD, OVP_DATA_KEY, OVP_JWT_SIGNING_KEY
                                            # (openssl rand -base64 32 for each key)
cp config/users.example.yaml config/users.yaml
docker compose run --rm api hash-password   # put the hashes into users.yaml
# put server.crt and server.key into ./certs
docker compose up -d --build
```

The details, including running behind a reverse proxy, are in [docs/deployment.md](docs/deployment.md).

## Roadmap

- [x] Profiles, tags, vault, synchronisation, personal preferences
- [x] Sign in with a name, a user file, LDAP or Active Directory, Entra ID
- [x] Roles, account revocation and the wipe directive
- [x] Docker Compose, logging, Swagger
- [ ] Remote mode in the client
- [ ] Rotating the data key in place
- [ ] Signing in with Entra ID measured against a real tenant

The server keeps no connection history. Sessions, durations and traffic stay on each client, as they
do today.

## Licence

MIT. See [LICENSE](LICENSE).

OpenVPN is a registered trademark of OpenVPN Inc. This is an independent project; it is not affiliated
with, endorsed by or supported by OpenVPN Inc.
