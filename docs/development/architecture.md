# Architecture

One ASP.NET Core project on .NET 10 with PostgreSQL, in three layers. See [the overview](README.md) for
the other development pages.

```
src/OpenVpnPilot.Server/
  Controllers/     bind the request, state who may call it, return what the service returns
  Services/        every decision: validation, permissions beyond the role, encryption, change numbers
  Repositories/    Entity Framework and nothing else
  Data/            DbContext, entities, their configuration, migrations
  Contracts/       the request and response records, the error codes and header names
  Mapping/         entity to contract, by hand
  Auth/            the four identity providers, token issuing, the current user
  Middleware/      request id, errors, HTTPS, mandatory headers, account revocation
  Security/        AES-GCM for data at rest, Argon2id for the user file, certificates
  Configuration/   the environment, read once into records and checked at start
  Logging/         Serilog, hourly files, retention
  Background/      hourly maintenance
  OpenApi/         Swagger
  Extensions/      service registration and the request pipeline
  Commands/        hash-password and the container health probe
```

Entities never leave the process: what crosses the wire is a record under `Contracts/`, mapped by hand
in `Mapping/`. Everything is behind an interface and injected through the constructor.

## The request pipeline

The order is in `Extensions/PipelineSetup.cs` and is the design:

1. Forwarded headers, in proxy mode only, so the client address and scheme are the proxy's account of
   them and only from the networks in `OVP_TRUSTED_PROXIES`.
2. Request id, then the security headers, then the request log.
3. Error handling, so every refusal after this point has one shape.
4. The HTTPS requirement, then Swagger when it is switched on.
5. The mandatory `X-Pilot-*` headers.
6. Routing, the rate limiters, authentication.
7. The account check, which compares every authenticated request with the account as it is now and is
   where the wipe directive and `auth.token_revoked` come from.
8. Authorisation, the controllers, the health endpoints, and a fallback that answers 404 in the same
   problem shape.

## Things worth knowing before changing anything

- **Refusals are exceptions.** A service throws `ServiceException` with a status and a code from
  `Contracts/ErrorCodes.cs`; the middleware turns it into problem details. Controllers contain no
  error handling. A new situation gets a new code, and a code is never renamed.
- **Synchronised writes** go through `IUnitOfWork.BeginSyncedWriteAsync`, which takes a PostgreSQL
  advisory lock and a number from the `change_seq` sequence. Anything a client synchronises carries
  that number, and a deletion leaves a tombstone with it. A write that bypasses this is invisible to
  clients. A synchronising read takes the same lock shared, so its cursor never names a change its
  answer lacks.
- **Concurrency** uses PostgreSQL's `xmin` as the version of a profile and of a settings document; it
  is the ETag, and a stale one is 412.
- **Secrets at rest** go through `ISecretCipher` with a context naming the row, so a value copied to
  another row does not decrypt. Refresh tokens are stored only as hashes.
- **Account state** is cached for 30 seconds per user and invalidated at once by anything this process
  changes, so a disable is noticed on the very next request.
- **Maintenance** runs every hour: expired refresh tokens and deletion records older than 90 days are
  removed, and day folders of the log older than the retention are deleted.
- **Log messages** are source generated with `[LoggerMessage]` in a `*Log.cs` class next to the code,
  like the client. Event ids by area are listed in `Logging/HostLog.cs`.
- **Configuration** is read once into records and checked at start, every problem at once, each
  naming its variable. See [configuration](../configuration.md).
