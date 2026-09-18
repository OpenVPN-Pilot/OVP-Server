# Changelog

All notable changes to this project are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[semantic versioning](https://semver.org/spec/v2.0.0.html).

Development happens on `dev`. `master` carries releases, and every entry under Unreleased moves into a
version heading when one is tagged. A release tag is `v<version>`, for example `v1.2.0`.

## [Unreleased]

### Added

- **The server itself**: a REST API under `/api/v1` for OpenVPN Pilot's remote mode, one ASP.NET Core
  project on .NET 10 with PostgreSQL, run by Docker Compose. The contract a client relies on is
  `docs/client-integration.md`.
- **Shared profiles and tags.** Everyone reads them and fetches configurations to connect;
  administrators create, change and delete them, one at a time or up to 500 in one batch. A
  configuration has to be self contained, and one that no client could use is refused with the reason:
  a file reference instead of an inline block, no way to verify the server, or an unclosed block.
  Duplicates are recognised by the same hash the client computes.
- **A shared vault** of sign ins, one per profile and realm, so a password is typed once for the whole
  team rather than once per person and machine. Every user may add a missing entry; only
  administrators replace or delete one.
- **Favourites, shortcuts and settings per person**, stored on the server and following them from
  machine to machine.
- **Synchronisation from a cursor.** One call returns what changed since the last one, deletions
  included, so a client keeps a local copy and connects without the server.
- **Four ways of signing in**, chosen with `OVP_AUTH_MODE`: a name only, a YAML user list with Argon2id
  hashes that is read again when it changes, LDAP or Active Directory over LDAPS or StartTLS with
  group based roles, and an exchange of Entra ID access tokens. Each ends in the server's own short
  lived access token and a single use refresh token bound to the installation.
- **The wipe directive.** A request made with the tokens of an account that was disabled or removed,
  by an administrator or at the identity provider, is answered with `X-Pilot-Directive: wipe`, and the
  client erases everything it received from this server.
- **Mandatory client headers** on every call: client version, API version, installation id, platform
  and a timestamp. Clients older than `OVP_MIN_CLIENT_VERSION` and requests whose timestamp is too far
  off are refused with a code that says so.
- **HTTPS only**, terminated by the server with a mounted certificate or by a reverse proxy whose
  addresses are named; anything that arrives in the clear is refused.
- Profile configurations and vault entries are encrypted with AES-GCM before they reach the database.
- Every refusal is problem details with a stable `code`, and every response carries the request id
  that finds its lines in the log.
- Logs on the console and in one file per hour under a folder per day, deleted after seven days;
  Docker's own copy of the console is capped by size.
- Swagger at `/swagger`, switched with `OVP_SWAGGER_ENABLED`, filling in the mandatory headers by
  itself when a request is tried by hand.
- `hash-password` for the user file and a health probe that needs no curl, both in the image.
- `OVP_ENTRA_INSTANCE` for tenants in a national cloud.
- **A test lab** in `lab/`: a Samba 4 Active Directory domain controller with users and nested groups,
  and a stand-in for Entra ID that mints tokens with chosen claims, both added to the Compose project
  with one extra file. Sign in with `ldap` and `entra` can be checked without a directory or tenant.

### Fixed

- Signing in against Active Directory failed with a server error. A search from the domain root is
  answered with references to the directory's other partitions besides the user, and those references
  were not expected. They are skipped.
- With Swagger switched off, `/swagger/index.html` and `/swagger/v1/swagger.json` answered 401
  instead of 404.
