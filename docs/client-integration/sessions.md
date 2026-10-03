# Signing in and keeping the session

A sign in, whichever mode the server runs in, ends the same way: the server's own short lived access
token and a single use refresh token, both bound to the installation that asked. Ask `server/info`
first to know which mode that is, see [headers](headers.md#first-contact). The operator's side of the
four modes is in [authentication](../authentication.md).

## Modes `none`, `file` and `ldap`

```http
POST /api/v1/auth/login
{ "username": "alice", "password": "..." }
```

`username` is 1 to 256 characters and `password` at most 1024. In mode `none` the password is ignored
and may be left out; in `file` and `ldap` an empty one is refused. The answer to every successful sign
in, refresh and Entra exchange has the same shape:

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

The access token is a signed JWT, but treat it as opaque: take the role and the expiry from the answer,
not from the token.

| Answer | Meaning |
| --- | --- |
| 401 `auth.invalid_credentials` | A wrong name or password, which cannot be told apart, and also a name with control characters and an account that is disabled in Active Directory |
| 403 `auth.forbidden` | Someone who has never signed in and is not allowed to: outside the group the operator names, or switched off in the user file |
| 401 `account.revoked` with the wipe directive | A known account that has been switched off, with the correct password. See [the wipe directive](wipe-directive.md) |
| 400 `auth.mode_mismatch` | The server signs in with Entra ID |
| 429 `request.too_many` | More attempts a minute from one address than the operator allows, 10 by default, sign in and Entra exchange together. `Retry-After` is 60 |
| 503 `auth.provider_unavailable` | The directory cannot be reached or is not set up as the server expects |

## Mode `entra`

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

`accessToken` is at most 16384 characters. The answer is the same token response as above. From here on
the client uses only this server's tokens; the Entra token is not needed again until the server asks
for a new sign in (see `auth.reauthentication_required` below). `POST /api/v1/auth/login` in this mode
answers 400 `auth.mode_mismatch`, and the exchange in any other mode does the same.

The exchange refuses with 401 `auth.invalid_credentials` a token the server did not accept: wrong
audience or issuer, expired, a bad signature, without the scope, requested by another application, or
without a user. It refuses with 403 `auth.forbidden` someone new who holds neither role nor group where
one is required, with 409 `auth.identity_conflict` a name that belongs to another account, and with
503 `auth.provider_unavailable` when Entra's signing keys cannot be fetched. A real Entra ID tenant has
not been tried yet; the exchange is proven against a stand-in, see
[authentication](../authentication.md#entra-entra-id).

## Storing the tokens

- The **refresh token** is a credential. Store it in the operating system keystore, never in a settings
  file.
- The **access token** can live in memory only.
- `user.role` decides what the interface offers, see [roles](README.md#roles).

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
  wait for its result. Two refreshes with the same token that arrive at the same moment count as reuse
  too: one gets the new pair, the other `auth.refresh_token_reused`, and the session ends for both.
- Refresh is limited to 30 calls a minute per installation (`X-Pilot-Client-Id`), separately from sign
  in, so a team behind one address is never throttled. A 429 here means the client refreshes in a loop.
- Every refresh asks the identity provider again whether the account still exists, is allowed in and in
  which role. A user file answers at once; a directory is asked on each refresh.
- 401 `auth.token_revoked` on an ordinary call means the account changed, for example its role, or an
  administrator signed it out, and the token was withdrawn. Refresh once and repeat; the new token
  carries the new role. When the refresh answers `auth.refresh_token_invalid`, sign in again.
- 401 `auth.refresh_token_invalid` (unknown, expired, signed out or revoked) or
  `auth.refresh_token_reused`: sign in again. Nothing is erased.
- 401 `auth.client_mismatch`: the token belongs to another installation. Sign in again on this one.
- 401 `auth.reauthentication_required`: the server wants the user to prove who they are again. In mode
  `entra` this happens after the period the operator sets, eight hours by default, and it is how a
  disabled Entra account is noticed. It also happens after the operator switched the server to another
  sign in mode. Sign in again; nothing is erased, and a user of the same name keeps their favourites,
  shortcuts and settings.
- 503 `auth.provider_unavailable`: the directory or Entra cannot be reached. Keep what is cached, keep
  working offline and try again later. This is never a reason to erase anything.
- 401 `account.revoked` with `X-Pilot-Directive: wipe`: see [the wipe directive](wipe-directive.md).

## Signing out

`POST /api/v1/auth/logout` with the refresh token ends this installation's session (204). It is also
204 for a token the server does not know and for one that belongs to another installation, which it
leaves alone. Discard both tokens afterwards whatever the answer. The access token itself stays valid
until it expires, at most one access token lifetime; the client simply stops using it.

Endpoints that need no access token and do not look at one: `server/info`, sign in, Entra exchange,
refresh and sign out. A client that sends its old access token along by default does no harm there, and
a revoked one cannot stop a refresh.
