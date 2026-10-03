# Endpoint reference

Every endpoint with what it takes, what it answers and how it refuses. Field names are camelCase JSON;
`?` after a type means it may be `null`; times are ISO 8601 with offset; ids are GUIDs. See
[the overview](README.md) for the other pages, and [errors](errors.md) for what every code means.

## Refusals every endpoint can give

Not repeated below. Every call under `/api/v1` except `server/info` can answer:

| Status | Code | When |
| --- | --- | --- |
| 400 | `transport.https_required` | Arrived over plain HTTP |
| 400 | `pilot.header_missing`, `pilot.header_invalid`, `pilot.api_version_unsupported`, `pilot.clock_skew` | A mandatory header is absent or wrong |
| 426 | `pilot.client_outdated` | Client older than `minimumClientVersion` |
| 400 | `request.validation_failed` | A field breaks its limit or the body cannot be read; `errors` names it |
| 404 | `request.not_found` | No such route, or a path id that is not a GUID |
| 413 | `request.too_large` | Body over the endpoint's limit |
| 500 | `server.error`, `server.data_key_mismatch` | A fault on the server; show the request id |

and every endpoint that needs a signed in user additionally:

| Status | Code | When |
| --- | --- | --- |
| 401 | `auth.token_missing`, `auth.token_invalid`, `auth.token_expired`, `auth.token_revoked`, `auth.client_mismatch` | See [sessions](sessions.md#staying-signed-in) |
| 401 + `X-Pilot-Directive: wipe` | `account.revoked` | See [the wipe directive](wipe-directive.md) |
| 403 | `auth.forbidden` | An administrator endpoint called by a user |

## Types

**Token** (answer of sign in, Entra exchange and refresh)

```json
{
  "accessToken": "string",
  "accessTokenExpiresAt": "2026-09-18T09:38:09+00:00",
  "refreshToken": "string",
  "refreshTokenExpiresAt": "2026-10-18T09:23:09+00:00",
  "user": { "id": "guid", "username": "string", "displayName": "string?", "role": "admin|user", "provider": "none|file|ldap|entra" }
}
```

**Server info**

```json
{ "name": "OpenVPN Pilot Server", "version": "string", "apiVersion": "1", "minimumClientVersion": "string",
  "authMode": "none|file|ldap|entra", "passwordRequired": true,
  "entra": { "tenantId": "string", "clientId": "string", "scope": "string", "authority": "string" } }
```

`entra` is `null` outside mode `entra`.

**Profile**

```json
{
  "id": "guid", "name": "string", "remoteHost": "string?", "remotePort": 1194, "protocol": "udp|tcp|null",
  "requiresCredentials": true, "hasUnsupportedOptions": false, "protectRoutes": null,
  "notes": "string?", "colour": "#RRGGBB|#RRGGBBAA|null", "tags": ["string"],
  "contentHash": "64 lower case hex", "changeSeq": 40, "eTag": "\"1234\"",
  "createdAt": "time", "createdBy": "string", "updatedAt": "time", "updatedBy": "string"
}
```

`remotePort` and `protocol` are `null` exactly when `remoteHost` is. `tags` are sorted regardless of
case.

**Configuration**: `{ "profileId": "guid", "contentHash": "string", "configuration": "string" }`

**Batch result**

```json
{ "created": 1, "duplicates": 1, "rejected": 1,
  "items": [ { "index": 0, "outcome": "created|duplicate|rejected", "profile": "Profile?", "code": "string?", "detail": "string?" } ] }
```

**Tag**: `{ "id": "guid", "name": "string", "colour": "string?", "changeSeq": 1 }`

**Vault entry**

```json
{ "profileId": "guid", "realm": "Auth", "username": "string?", "password": "string", "changeSeq": 41,
  "createdAt": "time", "createdBy": "string", "updatedAt": "time", "updatedBy": "string" }
```

**Changes**

```json
{ "cursor": 42, "full": false, "profiles": ["Profile"], "tags": ["Tag"], "vaultEntries": ["Vault entry"],
  "deletedProfiles": ["guid"], "deletedTags": ["guid"], "deletedVaultEntries": [ { "profileId": "guid", "realm": "string" } ] }
```

**Favourites**: `{ "items": [ { "profileId": "guid", "slot": 1 } ] }`, slotted ones first in slot order.

**Hotkeys**: `{ "items": [ { "actionId": "string", "gesture": "string", "profileId": "guid?", "isEnabled": true } ] }`, by `actionId`.

**Settings**: `{ "schemaVersion": 2, "document": { }, "eTag": "string?", "updatedAt": "time?" }`

**User** (administration)

```json
{ "id": "guid", "username": "string", "displayName": "string?", "role": "admin|user", "provider": "none|file|ldap|entra",
  "state": "active|disabled|deleted", "stateSource": "administrator|provider|null", "stateChangedAt": "time?",
  "createdAt": "time", "lastLoginAt": "time?", "lastSeenAt": "time?" }
```

## Server and health

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `GET /api/v1/server/info` | anyone, no `X-Pilot-*` headers | nothing | 200 Server info | nothing |
| `GET /health`, `/health/ready` | anyone, no headers | nothing | 200 `Healthy` when the database answers, else 503 | nothing |
| `GET /health/live` | anyone, no headers | nothing | 200 `Healthy` while the process runs | nothing |

## Signing in

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `POST /api/v1/auth/login` | anyone | `{ "username": "1..256", "password": "string? ..1024" }` | 200 Token | 400 `auth.mode_mismatch` (Entra server); 401 `auth.invalid_credentials`; 403 `auth.forbidden` (new user outside the allowed group); 401 `account.revoked` + wipe (known account switched off, correct password); 429 `request.too_many`; 503 `auth.provider_unavailable` |
| `POST /api/v1/auth/entra/exchange` | anyone | `{ "accessToken": "Entra token ..16384" }` | 200 Token | 400 `auth.mode_mismatch`; 401 `auth.invalid_credentials` (token not accepted); 403 `auth.forbidden`; 401 `account.revoked` + wipe; 409 `auth.identity_conflict`; 429; 503 |
| `POST /api/v1/auth/refresh` | anyone | `{ "refreshToken": "..512" }` | 200 Token, a new pair | 401 `auth.refresh_token_invalid`, `auth.refresh_token_reused`, `auth.client_mismatch`, `auth.reauthentication_required`; 401 `account.revoked` + wipe; 429 (30 a minute per installation); 503 |
| `POST /api/v1/auth/logout` | anyone | `{ "refreshToken": "..512" }` | 204, also for an unknown token | nothing beyond the common ones |
| `GET /api/v1/auth/me` | signed in | nothing | 200 `user` object of the Token | the common ones |

## Profiles

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `GET /api/v1/profiles` | signed in | query `tag` (exact name, any case), `search` (name, remote host or tag contains, any case), both optional | 200 `[Profile]` by name | the common ones |
| `GET /api/v1/profiles/{id}` | signed in | nothing | 200 Profile, header `ETag` | 404 `profile.not_found` |
| `GET /api/v1/profiles/{id}/configuration` | signed in | nothing | 200 Configuration | 404 `profile.not_found` |
| `POST /api/v1/profiles` | admin | body below | 201 Profile, `Location`, `ETag` | 400 `profile.invalid_configuration`, `profile.not_self_contained`, `profile.no_server_verification`, `request.validation_failed`; 409 `profile.duplicate` |
| `POST /api/v1/profiles/batch` | admin | `{ "items": [ body below, 1..500 ] }`, at most 64 MiB | 200 Batch result | 400 when `items` is missing, empty or over 500; 413 `request.too_large`. Every other problem is an item's `rejected` or `duplicate` outcome |
| `PUT /api/v1/profiles/{id}` | admin | header `If-Match`; body below with `configuration` null to keep the current one | 200 Profile, header `ETag` | 428 `request.precondition_required`; 412 `request.precondition_failed`; 404 `profile.not_found`; 409 `profile.duplicate`; the 400 codes of `POST` |
| `DELETE /api/v1/profiles/{id}` | admin | nothing | 204; its vault entries go with it | 404 `profile.not_found` |

Profile body:

| Field | Type | Rule |
| --- | --- | --- |
| `name` | string | required, 1 to 200 characters, no control characters |
| `configuration` | string | required on create, at most 256 KiB, self contained, see [profiles](profiles.md#how-the-server-reads-a-configuration) |
| `notes` | string? | at most 4000 characters; `null` clears |
| `colour` | string? | `#RRGGBB` or `#RRGGBBAA`; `null` clears |
| `protectRoutes` | bool? | `null` follows the client's setting |
| `tags` | [string]? | at most 50, each 1 to 100 characters, no control characters, no `null`; created when unknown; on `PUT` the complete list, and `null` or empty removes every tag |

## Tags

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `GET /api/v1/tags` | signed in | nothing | 200 `[Tag]` by name | the common ones |
| `POST /api/v1/tags` | admin | `{ "name": "1..100", "colour": "string?" }` | 201 Tag, `Location` | 409 `tag.duplicate` (any case) |
| `PUT /api/v1/tags/{id}` | admin | the same body | 200 Tag; a rename changes every profile carrying it | 404 `tag.not_found`; 409 `tag.duplicate` |
| `DELETE /api/v1/tags/{id}` | admin | nothing | 204; removed from every profile | 404 `tag.not_found` |

## Vault

`{realm}` is URL encoded, see [the shared vault](vault.md#realms).

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `GET /api/v1/vault` | signed in | nothing | 200 `[Vault entry]` | the common ones |
| `GET /api/v1/profiles/{id}/vault` | signed in | nothing | 200 `[Vault entry]` | 404 `profile.not_found` |
| `POST /api/v1/profiles/{id}/vault/{realm}` | signed in | `{ "username": "string? ..512", "password": "1..4096" }` | 201 Vault entry, `Location` | 404 `profile.not_found`; 409 `vault.entry_exists`; 400 for the realm |
| `PUT /api/v1/profiles/{id}/vault/{realm}` | admin | the same body | 200 Vault entry, created or replaced | 404 `profile.not_found`; 400 for the realm |
| `DELETE /api/v1/profiles/{id}/vault/{realm}` | admin | nothing | 204 | 404 `vault.entry_not_found`; 400 for the realm |

## Synchronisation

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `GET /api/v1/sync/changes` | signed in | query `since`: 0 or a cursor this server returned | 200 Changes | 400 for a negative `since`; 410 `sync.cursor_expired` |

## The caller's own data

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `GET /api/v1/me/favourites` | signed in | nothing | 200 Favourites | the common ones |
| `PUT /api/v1/me/favourites` | signed in | Favourites, up to 1000 items, `slot` 1 to 10 or `null` | 200 Favourites as stored | 400 for a slot or profile used twice or a `null` item; 404 `profile.not_found`; 409 `request.conflict` |
| `GET /api/v1/me/hotkeys` | signed in | nothing | 200 Hotkeys | the common ones |
| `PUT /api/v1/me/hotkeys` | signed in | Hotkeys, up to 200 items, `actionId` and `gesture` 1 to 100 characters | 200 Hotkeys as stored | 400 for an action used twice or a `null` item; 404 `profile.not_found`; 409 `request.conflict` |
| `GET /api/v1/me/settings` | signed in | nothing | 200 Settings | the common ones |
| `PUT /api/v1/me/settings` | signed in | `{ "schemaVersion": 0.., "document": { } }`, optional `If-Match` | 200 Settings | 400 when `document` is not an object or over 64 KiB; 412 `request.precondition_failed`; 409 `request.conflict` |

## User administration

| Call | Who | Takes | Answers | Refuses with |
| --- | --- | --- | --- | --- |
| `GET /api/v1/users` | admin | nothing | 200 `[User]` by name | the common ones |
| `GET /api/v1/users/{id}` | admin | nothing | 200 User | 404 `user.not_found` |
| `POST /api/v1/users/{id}/disable` | admin | nothing | 200 User | 404 `user.not_found`; 409 `user.self_modification` |
| `POST /api/v1/users/{id}/enable` | admin | nothing | 200 User | 404 `user.not_found` |
| `POST /api/v1/users/{id}/revoke-tokens` | admin | nothing | 200 User | 404 `user.not_found` |
| `DELETE /api/v1/users/{id}` | admin | query `purge`, default `false` | 204 | 404 `user.not_found`; 409 `user.self_modification` |
