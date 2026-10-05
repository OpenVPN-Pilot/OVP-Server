# Integrating a client

Everything a client needs to talk to OpenVPN Pilot Server: how to find out how to sign in, which
headers every call carries, how a session is kept alive, how the local copy stays in step, what every
error means, and what to do when the server says the account is gone. OpenVPN Pilot's server mode is
such a client; these pages describe the server's side of that conversation precisely enough to write
another.

The server never starts a tunnel. It hands out profiles, the credentials they need and each person's
own favourites, shortcuts and settings; connecting stays entirely the client's job, exactly as it is for
a profile that was imported from a file.

Swagger at `/swagger` on a running server shows the same API with every field, when the operator has
it switched on. These pages are the contract; where the two ever disagree, the page is wrong and gets
fixed.

## The pages

| Page | What it settles |
| --- | --- |
| [Headers and versioning](headers.md) | `server/info`, the mandatory `X-Pilot-*` headers, what every response carries, minimum client version |
| [Signing in and keeping the session](sessions.md) | The four sign in modes, tokens, refresh, sign out |
| [The wipe directive](wipe-directive.md) | What a client erases, and when it must |
| [Errors](errors.md) | The problem details shape and every code |
| [Profiles and tags](profiles.md) | Reading, importing and changing the shared profiles, and how a configuration is judged |
| [The shared vault](vault.md) | One sign in per profile and realm |
| [Synchronisation](synchronisation.md) | The change feed that keeps a local copy current |
| [Favourites, shortcuts and settings](personal-data.md) | What belongs to one person |
| [User administration](users.md) | Disabling, deleting and signing out users |
| [Endpoint reference](endpoint-reference.md) | Every endpoint with what it takes, answers and refuses with |
| [Implementing a client](implementing-a-client.md) | How the data maps onto a client's model, and what is easy to get wrong |

## A session at a glance

1. `GET /api/v1/server/info` when the user enters an address, and again at every start. It needs no
   headers and says how to sign in and which client versions are accepted.
2. Sign in: `POST /api/v1/auth/login`, or for Entra ID the Microsoft sign in followed by
   `POST /api/v1/auth/entra/exchange`. The answer is an access token and a single use refresh token.
3. `GET /api/v1/sync/changes?since=0` for the complete state, then again with the returned cursor at
   a regular interval. Fetch each changed configuration from
   `GET /api/v1/profiles/{id}/configuration`.
4. `GET /api/v1/me/favourites`, `/me/hotkeys` and `/me/settings` for what follows the person.
5. Connect with the local copy. When OpenVPN asks for a sign in the vault does not hold,
   `POST /api/v1/profiles/{id}/vault/{realm}` shares what worked.
6. `POST /api/v1/auth/refresh` before the access token expires; `POST /api/v1/auth/logout` to leave.
7. Whenever a response carries `X-Pilot-Directive: wipe`, erase everything received from this server.

## Conventions

- **HTTPS only.** Passwords, refresh tokens, configurations with private keys and vault entries travel
  in plain text inside TLS, so the server refuses anything that did not arrive over HTTPS
  (`transport.https_required`). Validate the certificate the normal way through the operating system's
  trust store. Never switch validation off; an operator with a private authority installs it on the
  machine, as for any other internal service.
- **JSON** with camelCase property names, UTF-8. Request bodies are `application/json`; errors are
  `application/problem+json`.
- **Nothing is cached.** Every `/api` response says `Cache-Control: no-store` and `Pragma: no-cache`; a
  client must not keep responses in an HTTP cache either, since they carry tokens, keys and
  passwords. Every response also carries `X-Content-Type-Options: nosniff` and
  `Referrer-Policy: no-referrer`, and `Strict-Transport-Security` over HTTPS.
- **Times** are ISO 8601 with an offset, always UTC from the server, for example
  `2026-09-18T09:23:09.4177416+00:00`.
- **Identifiers** are GUIDs. A path segment that is not a GUID where one is expected matches no route
  and is answered 404 `request.not_found`. Profiles created on the server have ids the client has
  never seen; see [Implementing a client](implementing-a-client.md).
- **Paths** all start with `/api/v1`. The version is in the path and in a header, and a server that
  speaks a different version refuses rather than guessing.
- **Body size.** A batch import may be up to 64 MiB, every other request up to 30 MB. A larger body is
  refused with 413 `request.too_large`. The server closes the connection while a client is still
  sending, so the client may see a reset connection instead of the answer; keep batches under the
  limit rather than relying on the answer.
- **Limits** are checked on the server and named in [the endpoint reference](endpoint-reference.md); a
  value over a limit is 400 `request.validation_failed` with the field in `errors`.

## Roles

There are two, and the identity provider decides which one a person has; this API never sets one.
`GET /api/v1/auth/me` and every token response carry it as `user.role`.

| | `user` | `admin` |
| --- | --- | --- |
| List and read profiles, tags, configurations | yes | yes |
| Read vault entries | yes | yes |
| Add a vault entry that does not exist yet | yes | yes |
| Replace or delete a vault entry | | yes |
| Create, change and delete profiles and tags, batch import | | yes |
| Own favourites, shortcuts and settings | yes | yes |
| Manage users | | yes |

A `user` should not see buttons that would only earn 403 `auth.forbidden`. For server profiles that
means no edit, rename, delete or import, while connecting and everything personal works as for a
local profile.
