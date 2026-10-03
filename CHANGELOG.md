# Changelog

All notable changes to this project are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[semantic versioning](https://semver.org/spec/v2.0.0.html).

Development happens on `dev`. `master` carries releases, and every entry under Unreleased moves into a
version heading when one is tagged. A release tag is `v<version>`, for example `v1.2.0`.

## [Unreleased]

## [1.0.0] - 2026-10-01

### Added

- **The server itself**: a REST API under `/api/v1` for OpenVPN Pilot's remote mode, one ASP.NET Core
  project on .NET 10 with PostgreSQL, run by Docker Compose. The contract a client relies on is
  `docs/client-integration/README.md`.
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

- `OVP_ENTRA_USER_GROUP`: members of a group may sign in as users, as an alternative to the user app
  role; with it set, nobody else may sign in.
- Every variable can be given as `<NAME>_FILE`, the path of a file holding the value, for Docker
  secrets.
- `/health`, the same check as `/health/ready`, and the container health check spelled out in the
  Compose file.
- A profile uploaded with `auth-user-pass <file>`, as the client's importer leaves it, is accepted and
  stored with a bare `auth-user-pass`; the file's user name and password belong in the vault. Before,
  such profiles were refused as not self contained.
- `request.too_large` (413): a batch import may be up to 64 MiB, and a larger body is refused in the
  same problem shape as every other refusal.
- Refreshes have their own limit, 30 a minute per installation, instead of sharing the sign in limit
  per address, so a team behind one address is not throttled.

### Security

- Answers under `/api` carry `Cache-Control: no-store`, so no proxy or HTTP cache keeps tokens,
  configurations or vault entries. Every answer carries `X-Content-Type-Options: nosniff` and
  `Referrer-Policy: no-referrer`, and answers over HTTPS carry `Strict-Transport-Security`.
- Entra ID tokens must have been requested by `OVP_ENTRA_CLIENT_ID` itself. Before, any application in
  the tenant that had been granted the scope could have signed people in.
- Entra ID users are recognised by their object id rather than their user principal name, which can be
  renamed and reassigned. A renamed account keeps its record; a name that reappears with another object
  id is refused with `auth.identity_conflict` instead of being handed the earlier person's record.
- An Entra ID user who has signed in before and loses the role or group is treated as disabled, and
  their clients are told to wipe themselves, as with LDAP.
- Profile and tag names may no longer contain control characters, which could forge lines in the log
  and entries in a client's list.
- Mode `none` warns at every start that anyone can sign in under any name.
- Two refreshes with the same token at the same moment both succeeded and left two valid sessions. The
  token is now claimed in one statement; the second is reuse, and the session ends.
- A user file that is empty, lists no users, or has a user without a name or password is refused and
  the previous list kept. Before, an emptied file removed everyone and told every client to wipe
  itself. A changed file is read once it was last written two seconds ago, so a file caught halfway
  through being saved is not taken for the new list.
- An LDAP group named in `OVP_LDAP_ADMIN_GROUP` or `OVP_LDAP_USER_GROUP` that the directory does not have
  answers 503 with an error naming the variable. Before, a typing error there disabled every user and
  wiped their clients. A user filter matching several entries no longer counts as a removed account.

### Fixed

- A synchronisation running while a change was being saved could answer with a cursor past that change
  without containing it, so the client never received it: measured, 202 of 1037 changes made during
  15 seconds were missing. The read now sees the state after the lock it waits for.
- A vault entry deleted and added again between two synchronisations was reported as both changed and
  deleted, and a client applying the answer in the documented order erased it.
- Sending an access token along with `server/info` answered 500, and sending a revoked one along with a
  refresh answered `auth.token_revoked`, so a client could never refresh. Anonymous endpoints no longer
  look at the account behind an access token.
- One bad item of a batch import, such as a name that is too long, refused the whole batch; every item
  is now judged on its own, including `null`. An item that was rejected no longer makes a later
  identical item a duplicate.
- A malformed `If-Match` was taken as absent: profile updates answered 428 and settings were silently
  overwritten. It now matches nothing and answers 412; `*` and lists of tags work as RFC 9110 says.
- The server read configurations differently from the client: the last `proto` and `port` instead of
  the first, quotes kept in the host, only `\n` as line end, case folded names. It now reads them as the
  client's parser does. `dh none` is accepted, credentials in an `<auth-user-pass>` block are refused,
  and a remote host over 255 characters is refused instead of failing with 500.
- A directory failing after the first connection, during a search or the user's bind, answered 500
  instead of 503 `auth.provider_unavailable`.
- `null` in a list of tags, favourites or shortcuts answered 500 instead of 400. A favourite naming a
  profile deleted at the same moment answers 409 `request.conflict` instead of 500.
- A realm with a slash was stored with `%2F` in it, and a realm with a space at either end was quietly
  trimmed and so never matched the client's keystore key; the first is decoded, the second refused.
- A client version such as `1.9` was taken as older than a minimum of `1.9.0`.
- A disable or token revocation could be undone for half a minute by a request that read the account
  just before it.
- An Entra ID token signed with a key published after the cached key list was refused until the list
  expired; the list is fetched again once.
- `OVP_ENTRA_TENANT_ID` given as a domain name, and `OVP_LDAP_CA_CERT_PATH` pointing at a file that is not a
  certificate, now stop the start with a message naming the variable.
- An unreadable log folder could stop the server.

- Signing in against Active Directory failed with a server error. A search from the domain root is
  answered with references to the directory's other partitions besides the user, and those references
  were not expected. They are skipped.
- With Swagger switched off, `/swagger/index.html` and `/swagger/v1/swagger.json` answered 401
  instead of 404.
