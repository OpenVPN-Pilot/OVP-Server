# OpenVPN Pilot Server

The server side of OpenVPN Pilot: a REST API that lets a team share one set of OpenVPN profiles, one
vault of the credentials those profiles need, and lets each person keep their favourites, shortcuts
and settings on the server rather than on one machine. The client stays the thing that connects; the
server never starts a tunnel and never sees one.

It is one ASP.NET Core project, runs in Docker Compose next to PostgreSQL, and is published as free
software.

---

## Architecture rules

- One project, layered as controller, service, repository.
  - **Controllers** bind the request, state who may call it and turn a service result into a
    response. No decisions, no queries.
  - **Services** hold every decision: validation, permissions beyond the role, encryption, what a
    change means for synchronisation.
  - **Repositories** talk to Entity Framework and nothing else. No rules, no logging of business events.
- Entities never leave the process. What crosses the wire is a record under `Contracts/`, mapped by
  hand in `Mapping/`.
- Dependency injection everywhere, everything behind an interface, constructor injection only. No
  static mutable state.
- `Nullable` enabled, warnings treated as errors.
- `async`/`await` throughout, every asynchronous method takes a `CancellationToken`, and the one a
  controller passes on is the request's own.
- Every failure a client can act on is RFC 9457 problem details with a stable `code`, listed in
  `Contracts/ErrorCodes.cs` and in `docs/client-integration/errors.md`. The text may change; the code may not.
- Configuration is read once into options classes and validated at start. A missing or malformed
  value stops the process with a message that names the variable, rather than failing on the first
  request that needs it.

## Size of a file

A file is as short as it can be and as long as it has to be. Around 200 lines is the point to ask
whether it is two things; 300 is the limit for anything written by hand. When a file grows past that,
split it along the seam it already has, into the folder where that kind of thing lives. Migrations
written by `dotnet ef` are exempt: they are generated, not written.

## Tests

There are no unit tests, deliberately. The server is thin: most of it is mapping, persistence and
framework configuration, where a unit test restates the code it covers. What is worth proving is
behaviour against a real database, a real directory and a real client, and that is done by hand
against the Compose stack and written down in `docs/development/testing.md`. Do not add a test project.

## Language and style

- Everything in this repository is English: identifiers, comments, commit messages, documentation,
  Swagger descriptions and error messages.
- Comments explain why, not what. One style only: `// Rotation makes a stolen refresh token single use.`
- No emoji anywhere in code or comments. No ASCII art. No banner or divider comments.
- XML documentation comments on every controller action and every contract, because Swagger is
  generated from them and is read by people writing clients.

## Neutrality

This repository is public. Keep it free of context about who uses it or why it was written.

- No usage scenarios, origin stories, organisation names, machine names, user names, tenant
  identifiers, domain names or internal hosts in code, comments, commit messages, documentation or
  sample data.
- One exception, and only one: the author is named in `LICENSE` and in the assembly metadata.
- Examples use invented names such as `example-site`, `vpn.example.com`, `dc=example,dc=com`, and
  addresses from the documentation ranges (`203.0.113.0/24`, `198.51.100.0/24`).

## Git

- `master` carries releases, `dev` is where work happens. Nothing is committed to `master` directly;
  it moves by merging `dev` when a version is tagged. A release tag is `v<version>`.
- Short, imperative, English commit subjects prefixed with a gitmoji code, and a body that says why.
- Examples: `:sparkles: add vault endpoints`, `:bug: refuse a refresh token that was already rotated`,
  `:recycle: split profile validation out of the service`, `:memo: document the wipe directive`,
  `:bookmark: version 1.0.0 and what changed in it`.
- Never commit profiles, keys, certificates, databases, user lists or `.env`. `.gitignore` enforces
  this; do not override it.

## Documentation

- A short README that says what this is and points at the rest, and one page per subject under
  `docs/`: `deployment.md`, `configuration.md`, `authentication.md`, `operations.md`, and the two
  subjects that are several pages, each a folder with a `README.md` that indexes it:
  `client-integration/` (`headers.md`, `sessions.md`, `wipe-directive.md`, `errors.md`, `profiles.md`,
  `vault.md`, `synchronisation.md`, `personal-data.md`, `users.md`, `endpoint-reference.md`,
  `implementing-a-client.md`) and `development/` (`architecture.md`, `building.md`, `testing.md`). A
  subject gets a page when it is a subject, not because there is more to say about one that already
  has one, and a page that grows past about 250 lines is split along its own seams.
- `docs/client-integration/` is the contract with the client. Every endpoint, header, error code and
  behaviour a client depends on is described there, and a change to any of them changes the page it
  belongs to in the same commit.
- Every environment variable is in `.env.example` and in `docs/configuration.md`, in the same commit
  that introduces it.
- `CHANGELOG.md` follows Keep a Changelog. Every change a deployer or a client developer would notice
  gets an entry under `Unreleased` in the same commit.
- `docs/` is not a place for design documents, meeting notes or anything dated.

## Working agreement

- **No workarounds.** When something is blocked or behaves unexpectedly, stop and report it with the
  evidence and the options. Do not route around it.
- No silent `catch`. No `#pragma warning disable` without a comment stating why.
- A TODO is not a solution.
- **Secrets never appear in logs, exports or git**, and in the database only encrypted. A secret is a
  password, a token, a signing or data key, and the content of a profile configuration, which carries
  private keys. Log that a secret was read and by whom, never what it was.
- Log generously. Every request, every login, every change and every refusal is written with who,
  what and why, because an operator reading the console is the only person who can explain a problem
  a client reports.
- Log messages are source generated with `[LoggerMessage]`, in a `*Log.cs` class next to the code
  that writes them, as in the client. Event ids are grouped by area; the ranges are listed at the top
  of `Logging/HostLog.cs`. Pass values, not the result of formatting them: enums, ids and paths go in
  as they are.

---

## Verified facts

Measured against the Compose stack on Docker Desktop for Windows with .NET 10 and PostgreSQL 17. These
are test results, not assumptions. Do not re-derive them, and correct this section if a measurement
ever contradicts it.

### The image's SDK is the reference build

The SDK in `mcr.microsoft.com/dotnet/sdk:10.0` was newer than the workstation's 10.0.101 and brought
CA1873, which flags a log argument that is computed at the call site, such as `state.ToString()`.
The local build passed and the image build failed. `docker compose build` is therefore the build that
decides, and log methods take enums, ids and `PathString` as they are.

### Validation attributes on records belong to the parameter

`[property: Required]` on a positional record compiles, and MVC then throws on every request that
binds the record: validation metadata on a property of a record's primary constructor is refused at
run time, not at build time. The attributes go on the parameter, `[Required] string Username`.

### The framework's RequestId replaces ours

ASP.NET Core opens a logging scope for every request with a property named `RequestId`, holding the
connection based trace id from before any middleware ran. Serilog lets the scope win over a
`LogContext` property of the same name, so every line the framework or a service logged showed the
connection id while the request log showed ours. The server's id is therefore `PilotRequestId`.

### Npgsql looks for Kerberos first

Without `GssEncryptionMode=Disable`, Npgsql tries GSS encryption on every new connection, fails to
load `libgssapi_krb5.so.2`, which the runtime image does not carry, and prints a library error on
start that reads like a failure. The connection then succeeds anyway. The option is set in
`DatabaseOptions`.

### The first migration logs an error

On an empty database EF Core queries `__EFMigrationsHistory` before creating it and logs the failure
at `Error`, then creates the table and migrates. That line on a first start is expected.

### A user file on a bind mount is noticed by its modification time

On Docker Desktop for Windows, changing `config/users.yaml` on the host changes the modification time
the container sees within a second. The store compares it at most every five seconds rather than
watching for file events, which do not cross every kind of bind mount, and reads a changed file once it
was last written two seconds ago. Measured: removing a user from the file answered that user's next
request with the wipe directive 2.2 seconds later; an emptied file and one with blank values were
refused and the previous list kept.

### A repeatable read snapshot is taken by the statement that waits for the lock

PostgreSQL takes a repeatable read transaction's snapshot when its first statement starts, which for
`pg_advisory_xact_lock_shared` is before it waits. A synchronisation that waited for a writer therefore
read the cursor after the write and the data before it. Measured with four writers and one reader for
15 seconds: the reader's copy missed 202 of 1037 tags. With read committed after the lock it matched
exactly.

### Two refreshes with one token both pass a read-then-write check

Measured: two simultaneous refreshes with the same token both answered 200 in four of five attempts
when the token was read, checked and then marked as replaced. Claiming it with one conditional
`UPDATE` makes the second one wait for the first one's row lock and find it replaced.

### A body over the limit closes the connection

Kestrel refuses a body over the endpoint's limit as soon as the declared length exceeds it, answers
413 and closes the connection. A client still sending, such as Python's urllib, then sees a reset
instead of the answer; curl with `Expect: 100-continue` shows the problem details.

### PostgreSQL's xmin is the concurrency token

`uint Version` with `IsRowVersion()` maps to the system column `xmin`, which the migration lists but
PostgreSQL does not create. Every update changes it and EF reads it back, so it serves as the ETag
without a column of its own. An update sent with an older value fails with a concurrency exception,
which `UnitOfWork` turns into 412.

### A refused directory certificate is an outage, not a fault

`osixia/openldap:1.5.0`, used for the first LDAP test, issues its server certificate from an authority
built into the image, and that authority expired on 15 January 2026. The server refused it, correctly,
and the refusal first surfaced as a 500 because `AuthenticationException` was not among the exceptions
that mean the directory cannot be reached. It is now, and answers `auth.provider_unavailable`; a domain
controller certificate from an authority that is not trusted answers the same.

### Active Directory answers a root search with referrals

Measured against Samba 4.17 as a domain controller in `lab/`. A subtree search from
`DC=corp,DC=example,DC=com` returns the user and, alongside, search result references to
`CN=Configuration`, `DC=DomainDnsZones` and `DC=ForestDnsZones`. Novell's client throws
`LdapReferralException` for each while enumerating, which made every sign in a 500 against Active
Directory while OpenLDAP, which returns no references, worked. Windows Server returns the same
references. `LdapDirectory` skips them: none of those partitions holds the domain's users.

Also measured there: `LDAP_MATCHING_RULE_IN_CHAIN` on the user's own entry finds membership through a
nested group; `userAccountControl` carries the disabled bit after `samba-tool user disable`; a deleted
account is simply not found; StartTLS on 389 works as LDAPS on 636 does; and .NET accepts Samba's
certificate, which names the host only in its common name and has no subject alternative name.

### SSL_CERT_DIR takes a list

The runtime image's .NET reads `SSL_CERT_DIR` as a colon separated list of folders of PEM files, without
the hashed names OpenSSL itself wants. `lab/` sets `/etc/ssl/certs:/lab/trust`, so the system
authorities stay trusted and the lab's are added. The folder is read when the process starts, so a
certificate authority that appears later is only trusted after a restart.

### A file based C# app has no reflection based JSON

`dotnet run mock.cs` builds with the settings meant for native compilation, and
`JsonSerializer.Serialize` of an anonymous type throws that reflection based serialisation is disabled.
The lab's Entra stand-in sets `JsonSerializerIsReflectionEnabledByDefault` and `PublishAot=false` in
its `#:property` lines.

### The default fallback route leaves out paths with a dot

`MapFallback` without a pattern uses `{*path:nonfile}`, which does not match `/swagger/index.html`.
With Swagger off, such a path matched no endpoint at all, and the fallback authorisation policy then
answered 401 `auth.token_missing` instead of 404. The fallback is mapped with `{*path}`.

### Port 8443 can be reserved on Windows

Hyper-V reserves port ranges on Windows hosts, and 8443 was among them: publishing it failed with
"access to a socket was not permitted". `OVP_PUBLISH_PORT` exists for this; the container port stays
8443.
