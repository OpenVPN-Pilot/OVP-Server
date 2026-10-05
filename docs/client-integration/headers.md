# Headers and versioning

How a client introduces itself, and how the server turns away what it cannot serve before it is
misread. See [the overview](README.md) for the other pages.

## First contact

`GET /api/v1/server/info` is anonymous and the only API call that needs no `X-Pilot-*` headers. Ask it
when the user enters a server address, and again at every start.

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
| `version` | The server's version. Show it in diagnostics; do not branch on it. |
| `apiVersion` | `1`. A client that does not speak it says so instead of carrying on. |
| `minimumClientVersion` | Older clients are refused with 426 `pilot.client_outdated`. Tell the user to update before they try to sign in. |
| `authMode` | `none`, `file`, `ldap` or `entra`. Decides the sign in screen, see [sessions](sessions.md). |
| `passwordRequired` | `true` in `file` and `ldap`, `false` in `none` and `entra`: show no password field. |
| `entra` | Only in mode `entra`: `tenantId`, `clientId`, `scope` and `authority` for the Microsoft sign in. |

`server/info` does not look at an `Authorization` header, so a client that sends its access token
along by default does no harm.

## Mandatory headers

Every other call under `/api/v1` carries these, including sign in and refresh:

| Header | Value | Refused with |
| --- | --- | --- |
| `X-Pilot-Client-Version` | The client's own version, at least major and minor, for example `1.9.0`. A suffix such as `-beta.1` is allowed and ignored when comparing, and `1.9` equals `1.9.0`. | 400 `pilot.header_missing` / `pilot.header_invalid`, 426 `pilot.client_outdated` |
| `X-Pilot-Api-Version` | `1` | 400 `pilot.api_version_unsupported` |
| `X-Pilot-Client-Id` | A GUID generated once per installation and kept for good, next to the settings. Not per user and not per server. The all zero GUID is refused. | 400 `pilot.header_invalid` |
| `X-Pilot-Platform` | `windows`, `macos` or `linux`, in any case | 400 `pilot.header_invalid` |
| `X-Pilot-Timestamp` | The moment the request is sent, ISO 8601 UTC, for example `2026-09-18T09:23:09Z`. A value without an offset is read as UTC. | 400 `pilot.clock_skew` when it is further off the server's clock than the operator allows, five minutes by default |
| `X-Pilot-Request-Id` | Optional. 8 to 64 letters, digits or dashes. | never; an unusable value is replaced |

The checks run in this order and the first failure is the answer: API version, client version
(including the minimum), client id, platform, timestamp. A client that sends nothing at all is
therefore told about `X-Pilot-Api-Version` first.

The client id matters beyond identification: tokens are bound to it. An access or refresh token used
with another client id is refused with `auth.client_mismatch`, so a token copied off one machine is
worthless on another. Keep the id stable across updates and reinstallation of the same user profile.

`pilot.clock_skew` is a problem with the machine's clock, not with the server. Its detail names both
times; show that to the user rather than retrying.

## What every response carries

| Header | Value |
| --- | --- |
| `X-Pilot-Request-Id` | The one sent, or a new one |
| `X-Pilot-Server-Version` | The server's version |
| `X-Pilot-Api-Version` | `1` |

Put the request id into the client's own log and into any error shown to the user: the operator finds
every server log line of that request by it. An error body repeats it as `requestId`, see
[errors](errors.md).

## Versions

- The API version is `1`, in the path (`/api/v1`) and in `X-Pilot-Api-Version`. A request for another
  version is refused with `pilot.api_version_unsupported` instead of being answered in a shape the
  client does not expect.
- The minimum client version is the operator's setting, `OVP_MIN_CLIENT_VERSION`. It is how an operator
  retires clients that no longer behave correctly against the server. A refused client can still call
  `server/info`, which is why a client reads it before anything else.
- The server logs the client version and the client id with every request.
