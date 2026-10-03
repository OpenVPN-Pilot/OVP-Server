# User administration

Administrators only, for an administration screen if the client offers one. See
[the overview](README.md) for the other pages and
[the endpoint reference](endpoint-reference.md#user-administration) for every status code.

A user exists on the server from the first successful sign in. A user's role comes from the identity
provider (the user file, the directory group, the Entra role or group, or the list of administrators in
mode `none`), not from this API.

| Call | Effect |
| --- | --- |
| `GET /api/v1/users`, `GET /api/v1/users/{id}` | Everyone who has signed in, by name, with state and last activity |
| `POST /api/v1/users/{id}/disable` | Disables; their clients get the wipe directive on their next request |
| `POST /api/v1/users/{id}/enable` | Enables a disabled or deleted user |
| `POST /api/v1/users/{id}/revoke-tokens` | Signs them out everywhere without erasing anything |
| `DELETE /api/v1/users/{id}` | Deletes; the wipe directive as with disable. The record stays so the name cannot simply sign in again |
| `DELETE /api/v1/users/{id}?purge=true` | Also removes the record and their favourites, shortcuts and settings |

A user:

| Field | Meaning |
| --- | --- |
| `state` | `active`, `disabled` or `deleted`. A client of a disabled or deleted user is told to wipe itself. |
| `stateSource` | `administrator` when an administrator set the state, `provider` when the identity provider reported it, otherwise `null`. |
| `stateChangedAt` | When the state last changed. |
| `role`, `provider` | As the identity provider reported them at the last sign in or refresh. |
| `createdAt`, `lastLoginAt`, `lastSeenAt` | The first sign in, the last one, and the last request, to within a few minutes. |

- A state an administrator set only an administrator reverses, with `enable`. A state the identity
  provider set is lifted again when the provider reports the account as active.
- Disabling and deleting also end every session of the user. Neither can be applied to the
  administrator's own account (409 `user.self_modification`), because nobody would be left to undo it.
- After a purge the same name signs in again as a new user. Without a purge it cannot, in any mode, until
  an administrator enables it. Delete first and purge once the clients have had time to be told, see
  [the wipe directive](wipe-directive.md#when-it-is-not-sent).
