# Implementing a client

How the server's data maps onto a client's model, what OpenVPN Pilot's own server mode does with it, and
the things that are easy to get wrong. See [the overview](README.md) for the other pages.

## Mapping onto OpenVPN Pilot's model

| Client | Server |
| --- | --- |
| `Profile.Id` | `profile.id`. A server profile keeps the server's id locally, so vault references and favourites line up. |
| `Profile.Source` | `Server`: a copy of a profile the configured server holds, kept current by synchronisation. |
| `Profile.Configuration`, `ContentHash` | `configuration`, `contentHash` |
| `RemoteHost`, `RemotePort`, `Protocol`, `RequiresCredentials`, `HasUnsupportedOptions`, `ProtectRoutes`, `Notes`, `Colour` | same names |
| `Profile.Tags` | `tags` (names) |
| `Profile.IsFavourite`, `FavouriteSlot` | `/me/favourites`, per user |
| `HotkeyBinding` (`ActionId`, `Gesture`, `ProfileId`, `IsEnabled`) | `/me/hotkeys` items |
| `settings.json` | `/me/settings`, the portable part only |
| `ISecretStore` entry `profile/{id:N}/{realm}` | vault entry `(profileId, realm)` |
| `PackagedCredential` (`ProfileId`, `Realm`, `Username`, `Password`) | vault entry, the same four fields |
| `ISecretStore` entry for the server's refresh token | the refresh token of [the session](sessions.md#storing-the-tokens) |
| `Session` history | stays local; the server keeps no history |
| `Profile.LastConnectedAt`, `ConnectCount` | stay local, per machine |

## What OpenVPN Pilot does with it

OpenVPN Pilot keeps the copy of a server's profiles in a database of its own, one per server, and works
from it whenever the server cannot be reached. A synchronisation sends the changes made here first and
then pulls the server's: profiles, tags and shared sign ins from [the change feed](synchronisation.md),
then favourites, shortcuts and settings. It runs at start, every two minutes, shortly after a local
change, when the network returns and on request; while the server is unreachable it tries again after
5, 15 and 30 seconds and then every 60. Signing out, like [the wipe directive](wipe-directive.md),
removes everything that came from the server.

The client's own documentation, at [OVP-Client](https://github.com/OpenVPN-Pilot/OVP-Client), says
the same from its side. The server does not depend on any of it; a client that follows the contract in
these pages behaves correctly towards the server.

## Things a client has to get right

- **Deleting a server profile deletes its keystore entries.** A deletion in the feed and the wipe
  directive both have to remove every stored sign in under the profile's id, not only the profile.
- **The local copy is identified by the server's id**, not by the hash: a server profile can carry the
  same configuration as a local import, and the two are different profiles.
- **Upload goes through the importer that inlines files.** The server additionally rewrites
  `auth-user-pass <file>` and answers with the stored configuration, which the client keeps as the
  server copy, see [profiles](profiles.md#how-the-server-reads-a-configuration).
- **One refresh at a time**, behind a lock, and the new refresh token stored before the new access token
  is used, see [sessions](sessions.md#staying-signed-in).
- **One client id per installation**, generated once and kept next to the settings; tokens do not work
  under another.
- **Offline is not revoked.** `auth.provider_unavailable`, a refused connection and a timeout never
  erase anything; only the wipe directive does.
- **The request id** of every failed call goes into the client's log and into what the user sees.
- **Plain HTTP and certificate errors are never worked around**; the operator fixes the certificate.
- **Read `server/info` first**, and show `minimumClientVersion` to the user before a refused client
  would only see an error.
