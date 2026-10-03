# Errors

Every refusal is RFC 9457 problem details, whichever part of the server it comes from: a missing
header, a failed sign in, a validation error, a conflict, a fault. See [the overview](README.md) for
the other pages.

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

Branch on `code`, never on `detail`: codes are stable, texts may change, and a code is never renamed or
reused. `errors` is only present for `request.validation_failed`, keyed by the field. `requestId` is the
`X-Pilot-Request-Id` of the response; show it with the error so the operator can find the log lines.

Besides the body, two headers matter: `X-Pilot-Directive: wipe` on `account.revoked` (see
[the wipe directive](wipe-directive.md)) and `Retry-After` on 429, which is always 60.

## Codes

| Code | Status | Meaning | What the client does |
| --- | --- | --- | --- |
| `request.validation_failed` | 400 | A field is missing, too long or malformed, or the body is not readable; `errors` names it | Fix the request; a defect in the client if it reaches a user |
| `request.not_found` | 404 | No such route, or an identifier in the path that is not a GUID | Defect |
| `request.precondition_required` | 428 | A profile update without `If-Match` | Send the ETag |
| `request.precondition_failed` | 412 | Someone changed it since it was read, or an `If-Match` that matches nothing | Read again, reapply, retry |
| `request.conflict` | 409 | Collided with a change at the same moment, or referred to a profile deleted at that moment | Retry; the retry then answers what is wrong |
| `request.too_large` | 413 | The body is larger than the endpoint accepts | Send less at once, for example split a batch |
| `request.too_many` | 429 | Too many sign in attempts from this address, or refreshes from this installation | Wait `Retry-After` seconds |
| `transport.https_required` | 400 | Arrived without HTTPS | Configuration error; use `https://` |
| `pilot.header_missing` | 400 | A mandatory header is absent | Defect |
| `pilot.header_invalid` | 400 | A mandatory header is malformed | Defect |
| `pilot.api_version_unsupported` | 400 | Different API version | Tell the user client and server do not match |
| `pilot.client_outdated` | 426 | Client older than `minimumClientVersion` | Ask the user to update |
| `pilot.clock_skew` | 400 | The machine's clock is off | Tell the user to fix the clock |
| `auth.invalid_credentials` | 401 | Wrong name or password, or an Entra token that was not accepted | Ask again |
| `auth.mode_mismatch` | 400 | Password sign in on an Entra server or the reverse | Read `server/info` again |
| `auth.provider_unavailable` | 503 | Directory or Entra unreachable, or the directory not set up as the server expects | Stay offline, retry later; never wipe |
| `auth.forbidden` | 403 | Not allowed: not an administrator, or someone new outside the allowed group or role | Hide the action; explain |
| `auth.identity_conflict` | 409 | Entra ID: this name belongs to another account on the server | Explain; an administrator has to act |
| `auth.token_missing` | 401 | No access token | Sign in |
| `auth.token_invalid` | 401 | Token malformed or not signed by this server | Refresh; if that fails, sign in |
| `auth.token_expired` | 401 | Access token expired | Refresh and repeat |
| `auth.token_revoked` | 401 | Account changed or signed out, token withdrawn | Refresh and repeat; when that fails, sign in |
| `auth.client_mismatch` | 401 | Token belongs to another installation | Sign in again on this one |
| `auth.refresh_token_invalid` | 401 | Refresh token unknown, expired, signed out or revoked | Sign in |
| `auth.refresh_token_reused` | 401 | Refresh token used twice; session ended | Sign in; check the client never refreshes in parallel |
| `auth.reauthentication_required` | 401 | Prove identity again | Sign in |
| `account.revoked` | 401 | Account disabled or removed; comes with the wipe directive | [Wipe](wipe-directive.md) |
| `profile.not_found` | 404 | No such profile | Drop it locally |
| `profile.duplicate` | 409 | Another profile has exactly this configuration | Show which one |
| `profile.invalid_configuration` | 400 | Empty, larger than 256 KiB, an unclosed block, a remote host over 255 characters, or credentials in an `<auth-user-pass>` block | Show the detail |
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

## Which refusals depend on what

- **Before anything else**, in this order: HTTPS (`transport.https_required`, except for the health
  endpoints), then the mandatory headers (the `pilot.*` codes). An answer that is only about those is
  the same whatever endpoint was called.
- **Then the access token**: `auth.token_missing`, `auth.token_invalid`, `auth.token_expired`, and for a
  token that was accepted, the account behind it: `auth.token_revoked`, `auth.client_mismatch` and
  `account.revoked`. Endpoints that need no token skip this step even when one is sent.
- **Then the role**: `auth.forbidden` for an administrator endpoint called by a user.
- **Then the request itself**: `request.validation_failed` and the endpoint's own codes.

The [endpoint reference](endpoint-reference.md#refusals-every-endpoint-can-give) lists what each
endpoint adds.
