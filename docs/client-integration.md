# Integrating the client

Everything a client needs to talk to OpenVPN Pilot Server: how to find out how to sign in, which
headers every call carries, how a session is kept alive, how the local copy stays in step, what every
error means, and what to do when the server says the account is gone.

The server never starts a tunnel. It hands out profiles, the credentials they need and each person's
own favourites, shortcuts and settings; connecting stays entirely the client's job, exactly as it is for
a profile that was imported from a file.

Swagger at `/swagger` on a running server shows the same API with every field, when the operator has
it switched on. This page is the contract; where the two ever disagree, this page is wrong and gets fixed.

## Contents

- [Transport](#transport)
- [First contact](#first-contact)
- [Mandatory headers](#mandatory-headers)
- [Signing in](#signing-in)
- [Staying signed in](#staying-signed-in)
- [The wipe directive](#the-wipe-directive)
- [Errors](#errors)
- [Roles](#roles)
- [Synchronisation](#synchronisation)
- [Profiles and tags](#profiles-and-tags)
- [The shared vault](#the-shared-vault)
- [Favourites, shortcuts and settings](#favourites-shortcuts-and-settings)
- [User administration](#user-administration)
- [Endpoint reference](#endpoint-reference)
- [Mapping onto the client's model](#mapping-onto-the-clients-model)

## Transport

- **HTTPS only.** Passwords, refresh tokens, configurations with private keys and vault entries travel
  in plain text inside TLS, so the server refuses anything that did not arrive over HTTPS
  (`transport.https_required`). Validate the certificate the normal way through the operating system's
  trust store. Never switch validation off; an operator with a private authority installs it on the
  machine, as for any other internal service.
- **JSON** with camelCase property names, UTF-8. Request bodies are `application/json`; errors are
  `application/problem+json`.
- **Nothing is cached.** Every `/api` response says `Cache-Control: no-store`; a client must not keep
  responses in an HTTP cache either, since they carry tokens, keys and passwords.
- **Times** are ISO 8601 with an offset, always UTC from the server, for example
  `2026-09-18T09:23:09.4177416+00:00`.
- **Identifiers** are GUIDs. Profiles created on the server have ids the client has never seen; see
  [Mapping](#mapping-onto-the-clients-model).
- All paths start with `/api/v1`. The version is in the path and in a header, and a server that
  speaks a different version refuses rather than guessing.

## First contact

`GET /api/v1/server/info` is anonymous and the only API call that needs no `X-Pilot-*` headers.
Ask it when the user enters a server address, and again at every start.

```json
{
  "name": "OpenVPN Pilot Server",
  "version": "1.0.0",
  "apiVersion": "1",
  "minimumClientVersion": "1.9.0",
  "authMode": "ldap",
  "passwordRequired": true,
  "entra": null
}
```

| Field | Use |
| --- | --- |
| `name` | Always `OpenVPN Pilot Server`. Anything else means the address points somewhere else. |
| `apiVersion` | `1`. A client that does not speak it says so instead of carrying on. |
| `minimumClientVersion` | Older clients are refused with 426 `pilot.client_outdated`. Tell the user to update before they try to sign in. |
| `authMode` | `none`, `file`, `ldap` or `entra`. Decides the sign in screen, see below. |
| `passwordRequired` | `false` in `none` and `entra`: show no password field. |
| `entra` | Only in mode `entra`: `tenantId`, `clientId`, `scope` and `authority` for the Microsoft sign in. |

## Mandatory headers

Every other call under `/api/v1` carries these, including sign in and refresh:

| Header | Value | Refused with |
| --- | --- | --- |
| `X-Pilot-Client-Version` | The client's own version, for example `1.9.0`. A suffix such as `-beta.1` is allowed and ignored when comparing. | 400 `pilot.header_missing` / `pilot.header_invalid`, 426 `pilot.client_outdated` |
| `X-Pilot-Api-Version` | `1` | 400 `pilot.api_version_unsupported` |
| `X-Pilot-Client-Id` | A GUID generated once per installation and kept for good, next to the settings. Not per user and not per server. | 400 `pilot.header_invalid` |
| `X-Pilot-Platform` | `windows`, `macos` or `linux` | 400 `pilot.header_invalid` |
| `X-Pilot-Timestamp` | The moment the request is sent, ISO 8601 UTC, for example `2026-09-18T09:23:09Z` | 400 `pilot.clock_skew` when it is further off the server's clock than the operator allows, five minutes by default |
| `X-Pilot-Request-Id` | Optional. 8 to 64 letters, digits or dashes. | never; an unusable value is replaced |

The client id matters beyond identification: tokens are bound to it. An access or refresh token used
with another client id is refused with `auth.client_mismatch`, so a token copied off one machine is
worthless on another. Keep the id stable across updates and reinstallation of the same user profile.

`pilot.clock_skew` is a problem with the machine's clock, not with the server. Its detail names both
times; show that to the user rather than retrying.

Every response carries `X-Pilot-Request-Id` (the one sent, or a new one), `X-Pilot-Server-Version`
and `X-Pilot-Api-Version`. Put the request id into the client's own log and into any error shown to the
user: the operator finds every server log line of that request by it.

## Signing in

### Modes `none`, `file` and `ldap`

```http
POST /api/v1/auth/login
{ "username": "alice", "password": "..." }
```

In mode `none` the password is ignored and may be left out. The answer to every successful sign in,
refresh and Entra exchange has the same shape:

```json
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresAt": "2026-09-18T09:38:09+00:00",
  "refreshToken": "wS3m...",
  "refreshTokenExpiresAt": "2026-10-18T09:23:09+00:00",
  "user": {
    "id": "01a0b3d3-6064-75e9-a940-c28df3732a05",
    "username": "alice",
    "displayName": "Alice Example",
    "role": "admin",
    "provider": "file"
  }
}
```

A wrong name or password is 401 `auth.invalid_credentials`, and the two cannot be told apart. An LDAP
account outside the group the operator allows is 403 `auth.forbidden`. More than a few attempts a
minute from one address are 429 `request.too_many` with `Retry-After`.

### Mode `entra`

The client signs in with Microsoft itself, as a public client with the authorisation code flow and
PKCE in the system browser (MSAL does this), using the values from `server/info`:

- authority: `entra.authority`
- client id: `entra.clientId`
- scope: `entra.scope`

It then trades the Entra access token for this server's tokens:

```http
POST /api/v1/auth/entra/exchange
{ "accessToken": "<the Entra access token>" }
```

The answer is the same token response as above. From here on the client uses only this server's
tokens; the Entra token is not needed again until Entra asks for a new sign in (see
`auth.reauthentication_required` below). `POST /api/v1/auth/login` in this mode answers 400
`auth.mode_mismatch`, and the exchange in any other mode does the same.

### Storing the tokens

- The **refresh token** is a credential. Store it in the operating system keystore through the
  existing `ISecretStore`, for example under `server/{server id}/refresh`, never in `settings.json`.
- The **access token** can live in memory only.
- `user.role` decides what the interface offers; see [Roles](#roles).

`GET /api/v1/auth/me` answers the same `user` object for the current token.

## Staying signed in

- An access token lives 15 minutes by default (`accessTokenExpiresAt` says exactly). Send it as
  `Authorization: Bearer <token>`.
- Refresh shortly before it expires, or when a call answers 401 `auth.token_expired`, then repeat the
  call once:

  ```http
  POST /api/v1/auth/refresh
  { "refreshToken": "wS3m..." }
  ```

- **A refresh token is single use.** Every refresh returns a new pair; store the new refresh token
  before using the new access token. Presenting a refresh token a second time is taken as theft: the
  whole session is ended and the answer is 401 `auth.refresh_token_reused`. The client must therefore
  never refresh twice at the same time. Put the refresh behind a single lock and let every other call
  wait for its result.
- 401 `auth.token_revoked` on an ordinary call means the account changed (for example its role) and
  the token was withdrawn. Refresh once and repeat; the new token carries the new role.
- 401 `auth.refresh_token_invalid` (unknown, expired or signed out) or `auth.refresh_token_reused`:
  sign in again. Nothing is erased.
- 401 `auth.reauthentication_required`: the server wants the user to prove who they are again. In
  mode `entra` this happens after the period the operator sets, eight hours by default, and it is how
  a disabled Entra account is noticed. It also happens after the operator switched the server to
  another sign in mode. Sign in again; nothing is erased.
- 503 `auth.provider_unavailable`: the directory or Entra cannot be reached. Keep what is cached,
  keep working offline and try again later.
- `POST /api/v1/auth/logout` with the refresh token ends this installation's session (204). Discard
  both tokens afterwards whatever the answer. The access token itself stays valid until it expires,
  at most one access token lifetime; the client simply stops using it.

## The wipe directive

When an account is disabled or deleted, by an administrator or by the identity provider (removed from
the user file, disabled in the directory, taken out of the group that grants access), the next request
the client makes with that account's tokens is answered:

```http
HTTP/1.1 401 Unauthorized
X-Pilot-Directive: wipe
Content-Type: application/problem+json

{ "code": "account.revoked", "detail": "This account has been disabled or removed. ...", ... }
```

It can arrive on any call: an ordinary request, a refresh, or a sign in with a correct password for
an account that has been switched off. **The header decides, not the status code.** Whenever
`X-Pilot-Directive: wipe` is present:

1. Disconnect every tunnel of a profile that came from this server.
2. Delete every profile that came from this server, and its materialised runtime configuration.
3. Delete every vault entry that came from this server from the keystore: the secrets stored under
   `profile/{profileId:N}/{realm}` for those profiles.
4. Delete this server's refresh token, the sync cursor, and the copies of favourites, shortcuts and
   settings that were received from it.
5. Remove the server from the client's list of servers, or mark it as signed out, and tell the user
   in plain words that the account no longer has access.
6. Do not retry, and do not touch anything that did not come from this server: locally imported
   profiles and their stored sign ins stay.

The directive is only ever sent in answer to a token the server itself signed, or to a correct
password, so a stranger cannot trigger it. It only travels over HTTPS. A client that is offline when
the account is removed receives it the next time it reaches the server.

## Errors

Every refusal is RFC 9457 problem details:

```json
{
  "type": "urn:openvpnpilot:error:profile.duplicate",
  "title": "profile.duplicate",
  "status": 409,
  "detail": "The profile 'Example Site A' has exactly this configuration.",
  "instance": "/api/v1/profiles",
  "code": "profile.duplicate",
  "requestId": "557d1488-153d-4f0e-9d6a-2f4c8f0a1b2c",
  "errors": { "Username": ["The Username field is required."] }
}
```

Branch on `code`, never on `detail`: codes are stable, texts may change. `errors` is only present
for `request.validation_failed`.

| Code | Status | Meaning | What the client does |
| --- | --- | --- | --- |
| `request.validation_failed` | 400 | A field is missing, too long or malformed; `errors` names it | Fix the request; a defect in the client if it reaches a user |
| `request.not_found` | 404 | No such route | Defect |
| `request.precondition_required` | 428 | A profile update without `If-Match` | Send the ETag |
| `request.precondition_failed` | 412 | Someone changed it since it was read | Read again, reapply, retry |
| `request.conflict` | 409 | Collided with a change at the same moment | Retry |
| `request.too_many` | 429 | Too many sign in attempts from this address | Wait `Retry-After` seconds |
| `transport.https_required` | 400 | Arrived without HTTPS | Configuration error; use `https://` |
| `pilot.header_missing` | 400 | A mandatory header is absent | Defect |
| `pilot.header_invalid` | 400 | A mandatory header is malformed | Defect |
| `pilot.api_version_unsupported` | 400 | Different API version | Tell the user client and server do not match |
| `pilot.client_outdated` | 426 | Client older than `minimumClientVersion` | Ask the user to update |
| `pilot.clock_skew` | 400 | The machine's clock is off | Tell the user to fix the clock |
| `auth.invalid_credentials` | 401 | Wrong name or password, or an Entra token that was not accepted | Ask again |
| `auth.mode_mismatch` | 400 | Password sign in on an Entra server or the reverse | Read `server/info` again |
| `auth.provider_unavailable` | 503 | Directory or Entra unreachable | Stay offline, retry later |
| `auth.forbidden` | 403 | Not allowed: not an administrator, or not in the allowed group | Hide the action; explain |
| `auth.identity_conflict` | 409 | Entra ID: this name belongs to another account on the server | Explain; an administrator has to act |
| `auth.token_missing` | 401 | No access token | Sign in |
| `auth.token_invalid` | 401 | Token malformed or not signed by this server | Refresh; if that fails, sign in |
| `auth.token_expired` | 401 | Access token expired | Refresh and repeat |
| `auth.token_revoked` | 401 | Account changed, token withdrawn | Refresh and repeat |
| `auth.client_mismatch` | 401 | Token belongs to another installation | Sign in again on this one |
| `auth.refresh_token_invalid` | 401 | Refresh token unknown, expired or signed out | Sign in |
| `auth.refresh_token_reused` | 401 | Refresh token used twice; session ended | Sign in; check the client never refreshes in parallel |
| `auth.reauthentication_required` | 401 | Prove identity again | Sign in |
| `account.revoked` | 401 | Account disabled or removed; comes with the wipe directive | [Wipe](#the-wipe-directive) |
| `profile.not_found` | 404 | No such profile | Drop it locally |
| `profile.duplicate` | 409 | Another profile has exactly this configuration | Show which one |
| `profile.invalid_configuration` | 400 | Empty, too large or an unclosed block | Show the detail |
| `profile.not_self_contained` | 400 | A directive names a file instead of an inline block | Import locally first, which inlines files |
| `profile.no_server_verification` | 400 | No `ca`, `capath`, `pkcs12` or `peer-fingerprint` | Show the detail |
| `tag.not_found` | 404 | No such tag | Refresh the tag list |
| `tag.duplicate` | 409 | A tag of that name exists | Show it |
| `vault.entry_not_found` | 404 | No such vault entry | Drop it locally |
| `vault.entry_exists` | 409 | The vault already has this sign in | Fetch it instead; only an administrator replaces it |
| `sync.cursor_expired` | 410 | The cursor cannot be answered | Synchronise from 0 |
| `user.not_found` | 404 | No such user | Refresh the list |
| `user.self_modification` | 409 | An administrator tried to disable or delete themselves | Explain |
| `server.error` | 500 | A fault on the server | Show the request id |
| `server.data_key_mismatch` | 500 | Stored data could not be decrypted | Show the request id; the operator must act |

## Roles

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
means no edit, rename, delete or import, while connecting and everything personal works as today.

## Synchronisation

The client keeps a local copy of the server's profiles, tags and vault so it can connect without the
server, and brings it up to date with one call:

```http
GET /api/v1/sync/changes?since=0
```

```json
{
  "cursor": 42,
  "full": true,
  "profiles": [ { "id": "...", "name": "Example Site A", "contentHash": "49c2...", "changeSeq": 40, "...": "..." } ],
  "tags": [ { "id": "...", "name": "Office", "colour": null, "changeSeq": 1 } ],
  "vaultEntries": [ { "profileId": "...", "realm": "Auth", "username": "vpnuser", "password": "...", "changeSeq": 41, "...": "..." } ],
  "deletedProfiles": [],
  "deletedTags": [],
  "deletedVaultEntries": []
}
```

1. The first time, and whenever told to start over, ask with `since=0`. The answer has `full: true` and
   is the complete state: anything the client holds from this server that is not in it no longer
   exists there and is removed locally.
2. Store `cursor` and pass it as `since` next time. The answer then holds only what changed after it,
   deletions included, with `full: false`.
3. For every profile in `profiles`: create or update the local copy's metadata. When `contentHash`
   differs from the one held, fetch `GET /api/v1/profiles/{id}/configuration` and replace the local
   configuration. The hash is lower case hex SHA-256 of the configuration text, computed exactly as
   `ProfileImporter.ComputeHash` does.
4. For every entry in `vaultEntries`: write the secret to the keystore under
   `profile/{profileId:N}/{realm}`, replacing what is there. The same reference the credential provider
   already reads, so connecting needs no change.
5. `deletedProfiles`: delete the profile, its runtime file and every keystore entry under its id.
   `deletedVaultEntries`: delete that keystore entry only. `deletedTags`: drop the tag.
6. 410 `sync.cursor_expired`: forget the cursor and start again from 0. It happens when a client was
   away for longer than deletion records are kept (90 days), or when the server was restored from a
   backup older than the cursor.

When to synchronise: at start, after signing in, after every change this client made, and on a timer
of a few minutes while running. A change another person makes reaches everyone within that interval.
The cursor is only valid for the server that issued it; keep one per server.

The call is cheap when nothing changed: an empty delta and the same cursor.

## Profiles and tags

### Reading

- `GET /api/v1/profiles` lists every profile without configurations. `?tag=Office` filters by tag,
  `?search=text` matches name, remote host and tag regardless of case, as the client's search does.
- `GET /api/v1/profiles/{id}` is one profile, with its ETag also in the `ETag` header.
- `GET /api/v1/profiles/{id}/configuration` is what the client connects with:

  ```json
  { "profileId": "...", "contentHash": "49c2...", "configuration": "client\ndev tun\n...<ca>\n...\n</ca>\n" }
  ```

  Every read of a configuration is recorded in the server log with who read it.

A profile:

| Field | Meaning |
| --- | --- |
| `id` | Stable. Also the key of its vault entries. |
| `name`, `notes`, `colour` | As on the client. `colour` is `#RRGGBB` or `#RRGGBBAA`. |
| `remoteHost`, `remotePort`, `protocol` | From the first `remote`, `rport`/`port` and `proto`, as `ProfileConfigurationFacts` derives them. `protocol` is `udp` or `tcp`. |
| `requiresCredentials` | The configuration has `auth-user-pass`. |
| `hasUnsupportedOptions` | The configuration runs a program (`up`, `down`, `tls-verify` and the rest of the client's `ScriptDirectives`). |
| `protectRoutes` | `null` follows the client's setting. |
| `tags` | Tag names. |
| `contentHash` | See synchronisation. |
| `changeSeq` | Change number of the last change. |
| `eTag` | For `If-Match`. |
| `createdAt`, `createdBy`, `updatedAt`, `updatedBy` | Who and when. |

### Changing (administrators)

- `POST /api/v1/profiles` creates one:

  ```json
  {
    "name": "Example Site A",
    "configuration": "client\n...",
    "notes": null,
    "colour": "#3366ff",
    "protectRoutes": null,
    "tags": ["Office", "Europe"]
  }
  ```

  The configuration must be self contained, which is what the client's importer produces: every
  `ca`, `cert`, `key`, `tls-auth`, `tls-crypt`, `pkcs12` and the rest inline, no `auth-user-pass`
  file. So the upload path is: import locally with the existing wizard, then send each accepted
  configuration. Tags that do not exist are created. 201 with the profile; 409 `profile.duplicate`
  when an identical configuration is stored.
- `POST /api/v1/profiles/batch` takes `{ "items": [ ...up to 500 of the above... ] }` and judges each
  item on its own:

  ```json
  { "created": 2, "duplicates": 1, "rejected": 1,
    "items": [ { "index": 0, "outcome": "created", "profile": { }, "code": null, "detail": null },
               { "index": 1, "outcome": "duplicate", "profile": null, "code": "profile.duplicate", "detail": "..." } ] }
  ```

  This is the server side of bulk import and maps onto the import wizard's review list.
- `PUT /api/v1/profiles/{id}` replaces name, notes, colour, route protection and tags, and the
  configuration when `configuration` is not null. It requires `If-Match: "<eTag>"`: without it 428,
  with a stale one 412, in which case read the profile again, reapply and retry.
- `DELETE /api/v1/profiles/{id}` deletes it together with its vault entries (204).

Tags: `GET /api/v1/tags`; administrators `POST`, `PUT /{id}` and `DELETE /{id}` with
`{ "name": "Office", "colour": "#ffaa00" }`. Names are unique regardless of case. Renaming or deleting a
tag counts as a change of every profile carrying it, so those arrive in the next synchronisation.

## The shared vault

One sign in per profile and realm, shared by everyone. The realm is OpenVPN's: `Auth` for the user
name and password of `auth-user-pass`, or the name of a private key for its passphrase, exactly the
`realm` of a `CredentialRequest`. It is part of the path and must be URL encoded.

- `GET /api/v1/vault` returns every entry of every profile; `GET /api/v1/profiles/{id}/vault` those
  of one profile. Synchronisation delivers them too.
- `POST /api/v1/profiles/{id}/vault/{realm}` with `{ "username": "vpnuser", "password": "..." }`
  adds an entry that does not exist yet. **Every user may do this.** 201 with the entry, or 409
  `vault.entry_exists` when one is there.
- `PUT` on the same path stores or replaces it; `DELETE` removes it. Administrators only.

The intended flow on the client:

1. Before connecting a server profile, look in the keystore as today. Synchronisation has usually
   put the shared entry there already.
2. When OpenVPN asks and nothing is stored, prompt as today. When the connection succeeds with what
   was typed and the vault has no entry for that realm, `POST` it, so nobody else has to type it. Do
   not post a one time code, and do not post what failed.
3. On 409, the vault gained an entry meanwhile: fetch it and store it locally.
4. When the shared entry stops working, a user can overwrite their local keystore copy as today;
   replacing the shared one is for an administrator, through `PUT`.

`username` is `null` for a passphrase. Secrets are encrypted at rest on the server and are never
written to its logs, only that someone read or changed them.

## Favourites, shortcuts and settings

These belong to the signed in person, not to the team, and follow them to every machine. Each list is
replaced as a whole.

- `GET` / `PUT /api/v1/me/favourites`

  ```json
  { "items": [ { "profileId": "...", "slot": 1 }, { "profileId": "...", "slot": null } ] }
  ```

  `slot` is 1 to 10 (`HotkeyActions.MaximumFavouriteSlot`, 10 being the zero key) or `null`, each
  slot at most once, each profile at most once. Only server profiles can be favourites here; local
  profiles keep their favourite flags locally.
- `GET` / `PUT /api/v1/me/hotkeys`

  ```json
  { "items": [ { "actionId": "ToggleQuickSwitcher", "gesture": "Control+Alt+V", "profileId": null, "isEnabled": true } ] }
  ```

  `actionId` is one of `HotkeyActions.All`, `gesture` is written as the client stores it. The server
  does not interpret either.
- `GET` / `PUT /api/v1/me/settings`

  ```json
  { "schemaVersion": 2, "document": { "general": { }, "appearance": { "theme": "Dark" }, "...": "..." } }
  ```

  `document` is the portable part of `PilotSettings`, exactly what `PilotSettingsTransfer.Export`
  produces, at most 64 KiB; apply it with `PilotSettingsTransfer.Import` so machine specific values
  stay. `schemaVersion` is `PilotSettings.SchemaVersion`. The answer carries an `eTag`; send it as
  `If-Match` to refuse overwriting a change another machine made (412), or leave it out to overwrite.
  A `GET` before anything was stored answers `schemaVersion` 0 and an empty document.

## User administration

Administrators only, for an administration screen if the client offers one:

| Call | Effect |
| --- | --- |
| `GET /api/v1/users`, `GET /api/v1/users/{id}` | Everyone who has signed in, with state and last activity |
| `POST /api/v1/users/{id}/disable` | Disables; their clients get the wipe directive on their next request |
| `POST /api/v1/users/{id}/enable` | Enables a disabled or deleted user |
| `POST /api/v1/users/{id}/revoke-tokens` | Signs them out everywhere without erasing anything |
| `DELETE /api/v1/users/{id}` | Deletes; the wipe directive as with disable. The record stays so the name cannot simply sign in again |
| `DELETE /api/v1/users/{id}?purge=true` | Also removes the record and their favourites, shortcuts and settings |

A user's role comes from the identity provider (the user file, the directory group, the Entra role or
group, or the list of administrators in mode `none`), not from this API.

## Endpoint reference

| Method | Path | Who | Success |
| --- | --- | --- | --- |
| GET | `/api/v1/server/info` | anyone, no headers | 200 |
| POST | `/api/v1/auth/login` | anyone | 200 |
| POST | `/api/v1/auth/entra/exchange` | anyone | 200 |
| POST | `/api/v1/auth/refresh` | anyone | 200 |
| POST | `/api/v1/auth/logout` | anyone | 204 |
| GET | `/api/v1/auth/me` | signed in | 200 |
| GET | `/api/v1/profiles` | signed in | 200 |
| GET | `/api/v1/profiles/{id}` | signed in | 200 |
| GET | `/api/v1/profiles/{id}/configuration` | signed in | 200 |
| POST | `/api/v1/profiles` | admin | 201 |
| POST | `/api/v1/profiles/batch` | admin | 200 |
| PUT | `/api/v1/profiles/{id}` | admin, `If-Match` | 200 |
| DELETE | `/api/v1/profiles/{id}` | admin | 204 |
| GET | `/api/v1/tags` | signed in | 200 |
| POST | `/api/v1/tags` | admin | 201 |
| PUT | `/api/v1/tags/{id}` | admin | 200 |
| DELETE | `/api/v1/tags/{id}` | admin | 204 |
| GET | `/api/v1/vault` | signed in | 200 |
| GET | `/api/v1/profiles/{id}/vault` | signed in | 200 |
| POST | `/api/v1/profiles/{id}/vault/{realm}` | signed in | 201 |
| PUT | `/api/v1/profiles/{id}/vault/{realm}` | admin | 200 |
| DELETE | `/api/v1/profiles/{id}/vault/{realm}` | admin | 204 |
| GET | `/api/v1/sync/changes?since={cursor}` | signed in | 200 |
| GET, PUT | `/api/v1/me/favourites` | signed in | 200 |
| GET, PUT | `/api/v1/me/hotkeys` | signed in | 200 |
| GET, PUT | `/api/v1/me/settings` | signed in | 200 |
| GET | `/api/v1/users` | admin | 200 |
| GET | `/api/v1/users/{id}` | admin | 200 |
| POST | `/api/v1/users/{id}/disable` | admin | 200 |
| POST | `/api/v1/users/{id}/enable` | admin | 200 |
| POST | `/api/v1/users/{id}/revoke-tokens` | admin | 200 |
| DELETE | `/api/v1/users/{id}?purge=` | admin | 204 |
| GET | `/health`, `/health/live`, `/health/ready` | anyone, no headers | 200 |

## Mapping onto the client's model

| Client | Server |
| --- | --- |
| `Profile.Id` | `profile.id`. Server profiles keep the server's id locally, so vault references and favourites line up. |
| `Profile.Source` | A new value such as `Server`, with `SourcePath` naming the server, so a wipe finds exactly what came from it. |
| `Profile.Configuration`, `ContentHash` | `configuration`, `contentHash` |
| `RemoteHost`, `RemotePort`, `Protocol`, `RequiresCredentials`, `HasUnsupportedOptions`, `ProtectRoutes`, `Notes`, `Colour` | same names |
| `Profile.Tags` | `tags` (names) |
| `Profile.IsFavourite`, `FavouriteSlot` | `/me/favourites`, per user |
| `HotkeyBinding` (`ActionId`, `Gesture`, `ProfileId`, `IsEnabled`) | `/me/hotkeys` items |
| `settings.json` | `/me/settings`, the portable part only |
| `ISecretStore` entry `profile/{id:N}/{realm}` | vault entry `(profileId, realm)` |
| `PackagedCredential` (`ProfileId`, `Realm`, `Username`, `Password`) | vault entry, the same four fields |
| `Session` history | stays local; the server keeps no history |

Where the client's code touches this: `IProfileStore`, `IHotkeyStore` and `ISettingsService` are the
seams a server backed implementation fits behind; `IProfileImportService` already has the prepare and
commit steps that map onto the batch call; `ISecretStore` stays the local store the vault fills; and
`ConnectionManager` keeps receiving the configuration text as it does now. `docs/usage.md` in the
client promises that nothing but the release check touches the network, and needs to say that a
configured server does too.
