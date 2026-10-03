# The shared vault

One sign in per profile and realm, shared by everyone, so a password is typed once for the whole team
rather than once per person and machine. See [the overview](README.md) for the other pages and
[the endpoint reference](endpoint-reference.md#vault) for every status code.

## Realms

The realm is OpenVPN's: `Auth` for the user name and password of `auth-user-pass`, or the name of a
private key for its passphrase, exactly the realm of OpenVPN's credential request. It is part of the
path and must be URL encoded, a slash as `%2F` (`Uri.EscapeDataString` does both). The server takes it
exactly as sent: 1 to 200 characters, no control characters, and no space at either end, which is
refused rather than trimmed, because a trimmed realm would never match the keystore key the client
looks up. A realm that breaks that is 400 `request.validation_failed`.

## Reading and writing

- `GET /api/v1/vault` returns every entry of every profile; `GET /api/v1/profiles/{id}/vault` those of
  one profile. [Synchronisation](synchronisation.md) delivers them too. Every read is recorded in the
  server log with who read it and how many entries it covered, never what they held.
- `POST /api/v1/profiles/{id}/vault/{realm}` with `{ "username": "vpnuser", "password": "..." }` adds an
  entry that does not exist yet. **Every user may do this.** 201 with the entry, or 409
  `vault.entry_exists` when one is there.
- `PUT` on the same path stores or replaces it; `DELETE` removes it. Administrators only. A `PUT` for an
  entry that does not exist creates it.

`password` is required, 1 to 4096 characters. `username` is at most 512 characters and `null` for a
passphrase; an empty one is stored as `null`. An entry:

```json
{ "profileId": "...", "realm": "Auth", "username": "vpnuser", "password": "...", "changeSeq": 41,
  "createdAt": "...", "createdBy": "alice", "updatedAt": "...", "updatedBy": "alice" }
```

A profile that does not exist is 404 `profile.not_found`; deleting an entry that does not exist is 404
`vault.entry_not_found`. Deleting a profile deletes its entries with it.

## The intended flow on the client

1. Before connecting a server profile, look in the keystore as for any other profile.
   Synchronisation has usually put the shared entry there already.
2. When OpenVPN asks and nothing is stored, prompt as usual. When the connection succeeds with what was
   typed and the vault has no entry for that realm, `POST` it, so nobody else has to type it. Do not
   post a one time code, and do not post what failed.
3. On 409, the vault gained an entry meanwhile: fetch it and store it locally.
4. When the shared entry stops working, a user can overwrite their local keystore copy; replacing the
   shared one is for an administrator, through `PUT`.

Secrets are encrypted at rest on the server with AES-GCM, bound to the profile and realm they belong to,
and are never written to its logs, only that someone read or changed them. They leave the server in
plain text inside TLS, so the client must store what it receives in the operating system keystore and
nowhere else.
