# Synchronisation

The client keeps a local copy of the server's profiles, tags and vault so it can connect without the
server, and brings it up to date with one call. See [the overview](README.md) for the other pages.

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

## Applying an answer

1. The first time, and whenever told to start over, ask with `since=0`. The answer has `full: true` and
   is the complete state: anything the client holds from this server that is not in it no longer
   exists there and is removed locally.
2. Store `cursor` and pass it as `since` next time. The answer then holds only what changed after it,
   deletions included, with `full: false`.
3. For every profile in `profiles`: create or update the local copy's metadata. When `contentHash`
   differs from the one held, fetch `GET /api/v1/profiles/{id}/configuration` and replace the local
   configuration. The hash is lower case hex SHA-256 of the configuration text as the server stores
   it, which is the text the client's own importer would hash, see
   [profiles](profiles.md#how-the-server-reads-a-configuration).
4. For every entry in `vaultEntries`: write the secret to the keystore under the profile and realm,
   replacing what is there, so connecting needs no change.
5. `deletedProfiles`: delete the profile, any configuration written to disk for it and every keystore
   entry under its id. `deletedVaultEntries`: delete that keystore entry only. `deletedTags`: drop the
   tag.
6. 410 `sync.cursor_expired`: forget the cursor and start again from 0.

Apply an answer in the order above, entries before deletions. An entry and a deletion of the same key
never arrive in one answer: a vault entry deleted and added again is reported as the entry alone.

## What a cursor is

The cursor is the highest change number the server had handed out when it answered. Every write to a
profile, tag or vault entry takes the next number, and everything up to the returned cursor is contained
in that answer or an earlier one. A client therefore never misses a change that was being saved while it
asked.

A cursor is only valid for the server that issued it; keep one per server. It cannot be answered, and the
server says 410 `sync.cursor_expired`, in two cases:

- The client was away for longer than deletion records are kept, 90 days. The server prunes them every
  hour, and a delta that would have to include a pruned deletion cannot be computed.
- The cursor is higher than anything this server has handed out, for example because the server was
  restored from a backup older than the cursor.

`since=0` always works, and a `since` below zero is 400.

## When to synchronise

At start, after signing in, after every change this client made, and on a timer of a few minutes while
running. A change another person makes reaches everyone within that interval. The call is cheap when
nothing changed: an empty delta and the same cursor.

## What is not in the feed

Favourites, shortcuts and settings are not part of this feed. Read `/me/favourites`, `/me/hotkeys` and
`/me/settings` at start, after signing in, and whenever a synchronisation reported deleted profiles,
since deleting a profile removes it from everyone's favourites and detaches it from shortcuts, see
[personal data](personal-data.md). Users and their state are not in it either; a change to the
account itself reaches the client as [the wipe directive](wipe-directive.md).

Every vault entry travels in the feed with its secret, for every profile, whoever asks. That is what
lets a client connect offline with the team's shared sign ins, and it is why the feed is as protected as
the vault itself.
