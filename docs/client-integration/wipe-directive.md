# The wipe directive

How the server tells a client that the account is gone and that everything the client received from
this server has to be erased. See [the overview](README.md) for the other pages.

## What the client sees

When an account is disabled or deleted, by an administrator or by the identity provider (removed from
the user file, disabled in the directory, taken out of the group that grants access), the next request
the client makes with that account's tokens is answered:

```http
HTTP/1.1 401 Unauthorized
X-Pilot-Directive: wipe
Content-Type: application/problem+json

{ "code": "account.revoked", "detail": "This account has been disabled or removed. ...", ... }
```

It can arrive on any call that carries the account's tokens: an ordinary request, a refresh, or a sign
in with a correct password for an account that has been switched off. **The header decides, not the
status code.** Whenever `X-Pilot-Directive: wipe` is present:

1. Disconnect every tunnel of a profile that came from this server.
2. Delete every profile that came from this server, and any configuration the client wrote to disk to
   connect it.
3. Delete every vault entry that came from this server: the shared sign ins stored for those profiles.
4. Delete this server's refresh token, the synchronisation cursor, and the copies of favourites,
   shortcuts and settings that were received from it.
5. Remove the server from the client's list of servers, or mark it as signed out, and tell the user in
   plain words that the account no longer has access.
6. Do not retry, and do not touch anything that did not come from this server: locally imported
   profiles and their stored sign ins stay.

The directive is only ever sent in answer to a token the server itself signed, or to a correct
password, so a stranger cannot trigger it. It only travels over HTTPS. A client that is offline when
the account is removed receives it the next time it reaches the server.

## When it is sent

| What happened | When the client is told |
| --- | --- |
| An administrator disabled or deleted the user | The next request with that account's tokens, whichever it is |
| The user was removed from the user file, or set `disabled: true` | The next request once the saved file has been read again, within a few seconds |
| The account was deleted or disabled in the directory, or taken out of the allowed group | At the next refresh, which a client makes about once per access token lifetime |
| An Entra ID user lost the role or group | At the next sign in with Entra, which the server forces every `OVP_ENTRA_REAUTH_HOURS` |

## When it is not sent

These are the cases where a client is told to sign in instead, and so keeps its copy until it does:

- The administrator purged the user (`DELETE /api/v1/users/{id}?purge=true`). A client still holding an
  unexpired access token is told to wipe on its next call, because the server no longer knows the
  account. Once the access token has expired, the server no longer recognises the account's refresh
  tokens either and answers `auth.refresh_token_invalid`. Deleting first and purging later, as
  [operations](../operations.md#taking-someones-access-away) describes, avoids the gap.
- An account disabled in Active Directory, when the client's refresh token has already expired. The
  directory refuses a disabled account's password, so it cannot be proven, and the answer is
  `auth.invalid_credentials`.
- A token that belongs to another installation: `auth.client_mismatch`, nothing else.
- The directory or Entra cannot be reached (`auth.provider_unavailable`): that says nothing about the
  account, and the client keeps working from what it has.
- The server was switched to another sign in mode: `auth.reauthentication_required`.

A client that is told to sign in and cannot, because the account is gone, should therefore offer the
user to remove this server and what came from it.
