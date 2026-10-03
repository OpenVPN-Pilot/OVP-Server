# Profiles and tags

The team's shared profiles and the tags that sort them. Everyone reads and connects; administrators
import, change and delete. See [the overview](README.md) for the other pages and
[the endpoint reference](endpoint-reference.md#profiles) for every status code.

## Reading

- `GET /api/v1/profiles` lists every profile without configurations, by name. `?tag=Office` filters by
  tag, `?search=text` matches name, remote host and tag regardless of case, as the client's search does.
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
| `remoteHost`, `remotePort`, `protocol` | From the configuration, see below. `protocol` is `udp` or `tcp`. All three are `null` when the configuration has no `remote`. |
| `requiresCredentials` | The configuration has `auth-user-pass`. |
| `hasUnsupportedOptions` | The configuration runs a program: `up`, `down`, `route-up`, `route-pre-down`, `ipchange`, `tls-verify`, `auth-user-pass-verify`, `client-connect`, `client-disconnect`, `learn-address` or `auth-user-pass-optional`. |
| `protectRoutes` | `null` follows the client's setting. |
| `tags` | Tag names, sorted regardless of case. |
| `contentHash` | Lower case hex SHA-256 of the stored configuration text. See [synchronisation](synchronisation.md). |
| `changeSeq` | Change number of the last change. |
| `eTag` | For `If-Match`. |
| `createdAt`, `createdBy`, `updatedAt`, `updatedBy` | Who and when. |

## How the server reads a configuration

The server reads lines, quotes, inline blocks and directives exactly as the client's configuration
parser does, and derives the list fields the way the client does:

- Lines end at `\r\n`, `\n` or a lone `\r`. Lines starting with `#` or `;` are comments.
- Arguments may be quoted with `"` or `'`; the quotes are not part of the value.
- Directive names are case sensitive and taken as written.
- `remoteHost` is the first argument of the first `remote`, at most 255 characters. `remotePort` is
  that remote's second argument, else the first `rport`, else the first `port`, else 1194. `protocol`
  is the remote's third argument, else the first `proto`; `tcp` when it starts with `tcp` regardless of
  case, else `udp`.
- Server verification counts as present with an inline `<ca>` or `<pkcs12>` block, or a `ca`, `capath`,
  `peer-fingerprint` or `pkcs12` directive. An inline `<peer-fingerprint>` block alone does not count,
  as it does not in the client.

A configuration is refused, with the reason in `detail`, when it is:

| Code | When |
| --- | --- |
| `profile.invalid_configuration` | Empty, over 256 KiB, a block that is never closed, a remote host over 255 characters, or a user name and password in an `<auth-user-pass>` block |
| `profile.not_self_contained` | `ca`, `cert`, `key`, `dh`, `extra-certs`, `pkcs12`, `crl-verify`, `secret`, `tls-auth`, `tls-crypt` or `tls-crypt-v2` names a file instead of an inline block. `dh none` is fine. |
| `profile.no_server_verification` | Nothing to verify the server with, so OpenVPN would refuse the configuration before connecting |

The upload path is therefore: import locally with the client's own importer, which inlines files, then
send each accepted configuration.

**The server stores the configuration in one respect differently from how it was sent:** a line
`auth-user-pass <file>` becomes a bare `auth-user-pass`. The file holds a user name and password, which
belong in the vault, and exists on no other machine; the bare directive makes OpenVPN ask, and the
client answers from the vault as for any other profile. Every other byte is kept. The answer's
`contentHash` is therefore the hash of what was stored, which differs from the local copy's hash when
the line was rewritten. Take the configuration and hash from the server for the server copy; sending
the same local profile again is still recognised as a duplicate.

## Changing (administrators)

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

  `name` is required, 1 to 200 characters, without control characters. `notes` is at most 4000
  characters. Tags that do not exist are created. 201 with the profile and a `Location`; 409
  `profile.duplicate` when an identical configuration is stored, naming the profile that has it.
- `POST /api/v1/profiles/batch` takes `{ "items": [ ...up to 500 of the above... ] }` and judges each
  item on its own:

  ```json
  { "created": 2, "duplicates": 1, "rejected": 1,
    "items": [ { "index": 0, "outcome": "created", "profile": { }, "code": null, "detail": null },
               { "index": 1, "outcome": "duplicate", "profile": null, "code": "profile.duplicate", "detail": "..." } ] }
  ```

  This is the server side of bulk import and maps onto an import wizard's review list. Every limit of a
  single profile is checked per item, so one item with a name that is too long, a malformed colour or
  even `null` is `rejected` with its code and detail, and the others are still created, together in one
  transaction. An item identical to an earlier item of the same batch that was created is a
  `duplicate`. The whole call is refused only when `items` is missing, empty or longer than 500 (400),
  or the body is over 64 MiB (413). Split a large import into batches by count and by size.
- `PUT /api/v1/profiles/{id}` replaces name, notes, colour, route protection and tags, and the
  configuration when `configuration` is not null. A `tags` that is `null` or empty removes every tag.
  It requires `If-Match: "<eTag>"`: without it 428, with a stale one 412, in which case read the
  profile again, reapply and retry. `If-Match` follows RFC 9110: `*` matches whatever is stored, a comma
  separated list matches when any of its tags does, and a malformed value matches nothing (412), it is
  never taken as absent. A new configuration identical to another profile's is 409 `profile.duplicate`.
- `DELETE /api/v1/profiles/{id}` deletes it together with its vault entries (204), removes it from
  everyone's favourites, and leaves any shortcut that named it in place with a `null` `profileId`.

## Tags

`GET /api/v1/tags` lists them by name. Administrators `POST`, `PUT /{id}` and `DELETE /{id}` with
`{ "name": "Office", "colour": "#ffaa00" }`. A name is 1 to 100 characters without control characters
and unique regardless of case (409 `tag.duplicate`); `colour` is optional.

A profile lists its tags by name, so renaming or deleting a tag counts as a change of every profile
carrying it, and those arrive in the next synchronisation. Changing only a tag's colour changes the
tag alone. Deleting a tag removes it from every profile.
