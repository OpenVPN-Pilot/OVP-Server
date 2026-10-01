# Running OpenVPN Pilot Server

## Logs

The server logs generously: every request with its status and duration, every sign in and refusal,
every change to a profile, tag, vault entry or user, every read of a configuration or a vault entry,
and every wipe directive it sends, each with who did it. It never logs a password, a token, a key or
the content of a configuration.

It writes to two places at once.

**The console**, in real time:

```bash
docker compose logs -f api
```

```
[09:23:09.417 INF] 7b1cfe77-... alice OpenVpnPilot.Server.Services.Profiles.ProfileImportService: alice created profile Example Site A (01a0b3d3-...), change 1
```

**Files**, one folder per day and one file per hour:

```
logs/
  2026-09-18/
    08.log
    09.log
  2026-09-19/
    ...
```

Each line carries the request id, the user, the client id and the client version:

```
2026-09-18 09:23:09.417 +00:00 [INF] request=7b1cfe77-... user=alice client=3f2c1b9e-... version=1.9.0 ...
```

The request id is the one a client receives in `X-Pilot-Request-Id` and shows in its own error
messages, so one search finds everything the server did for that request.

Day folders older than `OVP_LOG_RETENTION_DAYS` (seven by default) are deleted every hour. The day and
hour follow the container's time zone, `TZ`.

`OVP_LOG_LEVEL=Debug` adds the framework's own request handling and every SQL statement. That is a
lot; use it to chase a problem, not as a setting.

### What Docker keeps

Docker stores everything a container prints, independently of the files above, and without a limit
that store grows until the disk is full. Docker can only limit it by size, not by age, so the Compose
file keeps at most five files of 50 MB per container using the `local` driver. That is what
`docker compose logs` reads. The dated history with the seven day retention is the `logs` folder.

## Health

| Endpoint | Answers 200 when |
| --- | --- |
| `/health/live` | The process is serving requests |
| `/health`, `/health/ready` | It can also reach the database |

Both are anonymous, need no headers and are answered over plain HTTP in proxy mode, so a load
balancer can use them. The container's own health check calls `/health/live` through the server
binary, because the runtime image has no curl; `docker compose ps` shows the result, and the check is
spelled out in `docker-compose.yaml`. It asks `live` on purpose: a database outage is not something
restarting the API would fix. They are logged only at `Verbose`.

## Taking someone's access away

| Want | Do |
| --- | --- |
| Sign someone out everywhere, nothing else | `POST /api/v1/users/{id}/revoke-tokens` |
| Take access away and erase what their clients hold | `POST /api/v1/users/{id}/disable`, or remove them at the identity provider |
| The same, and record them as removed | `DELETE /api/v1/users/{id}` |
| Remove every trace of them | `DELETE /api/v1/users/{id}`, then later `DELETE /api/v1/users/{id}?purge=true` |

A disabled or deleted user's client is told to erase every profile, vault entry and setting it
received from this server on its next request, whichever request that is. A client that is offline
receives the instruction when it comes back. Profiles the user imported locally are not touched.

A purge removes the record of the user together with their sessions, so the server no longer recognises
their tokens: a client that comes back after a purge is told to sign in, not to erase itself. Delete
first, and purge once their clients have had time to call in, at the latest after the refresh token
lifetime (`OVP_REFRESH_TOKEN_DAYS`).

At the identity provider: removing a user from `users.yaml` or setting `disabled: true` acts on their
next request; removing them from the directory or the user group acts at their next refresh, at most
one access token lifetime later; in Entra ID at the next forced sign in, see
[authentication.md](authentication.md#entra-entra-id).

## Rotating keys

- **`OVP_JWT_SIGNING_KEY`**: change it and restart. Every session ends and everyone signs in again;
  nothing is lost.
- **`OVP_DATA_KEY`**: cannot be changed in place yet. Every stored configuration and vault entry is
  encrypted with it, and a server started with a different key answers `server.data_key_mismatch`
  for each of them.

## Maintenance the server does itself

Every hour: expired refresh tokens are removed, deletion records older than 90 days are pruned
(clients away longer start with a full synchronisation), and old log folders are deleted.
