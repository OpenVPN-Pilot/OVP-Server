# Favourites, shortcuts and settings

These belong to the signed in person, not to the team, and follow them to every machine. Each list is
replaced as a whole. See [the overview](README.md) for the other pages and
[the endpoint reference](endpoint-reference.md#the-callers-own-data) for every status code.

They are not part of [the change feed](synchronisation.md): read all three at start, after signing in,
and whenever a synchronisation reported deleted profiles.

## Favourites

`GET` / `PUT /api/v1/me/favourites`

```json
{ "items": [ { "profileId": "...", "slot": 1 }, { "profileId": "...", "slot": null } ] }
```

- `slot` is 1 to 10 (10 being the zero key) or `null`, each slot at most once, each profile at most
  once. Up to 1000 items.
- Only server profiles can be favourites here; local profiles keep their favourite flags locally. A
  profile that does not exist is 404 `profile.not_found`.
- The answer is the list as stored, slotted favourites first in slot order.
- When a profile is deleted, it disappears from every user's favourites.

## Shortcuts

`GET` / `PUT /api/v1/me/hotkeys`

```json
{ "items": [ { "actionId": "ToggleQuickSwitcher", "gesture": "Control+Alt+V", "profileId": null, "isEnabled": true } ] }
```

- `actionId` is the client's own action identifier and `gesture` is written as the client stores it, 1
  to 100 characters each. The server does not interpret either. Each `actionId` at most once, up to 200
  items, listed by `actionId`.
- `profileId` is optional, for actions that apply to a profile. One that does not exist is 404
  `profile.not_found`.
- When the profile a shortcut names is deleted, the shortcut stays and its `profileId` becomes `null`.

## Settings

`GET` / `PUT /api/v1/me/settings`

```json
{ "schemaVersion": 2, "document": { "general": { }, "appearance": { "theme": "Dark" }, "...": "..." } }
```

- `document` is the portable part of the client's settings, a JSON object of at most 64 KiB in UTF-8.
  The server keeps it and does not interpret it. It is stored as `jsonb`, so key order and white space
  are not preserved. A client applies it so that machine specific values stay machine specific.
- `schemaVersion` is the client's own settings schema version, 0 or more, so a newer client can migrate
  what an older one stored.
- The answer carries an `eTag` and `updatedAt`. Send the `eTag` as `If-Match` to refuse overwriting a
  change another machine made (412 `request.precondition_failed`), or leave `If-Match` out to
  overwrite. `If-Match` before anything was stored, or a malformed one, is 412 as well.
- A `GET` before anything was stored answers `schemaVersion` 0, an empty `document`, `eTag` null and
  `updatedAt` null.
- A `document` that is not an object, or over 64 KiB, is 400 `request.validation_failed`. A save that
  collides with another at the same moment is 412, and 409 `request.conflict` when it is the very
  first save of both.

## What stays on the machine

Usage figures such as the last connection and the number of connections are not kept on the server.
They describe one machine's use, drive that machine's "recent" list, and recording them centrally would
make the server keep the connection history it deliberately does not keep. Session history stays on
each client in the same way.
