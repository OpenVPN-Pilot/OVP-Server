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
  `Contracts/ErrorCodes.cs` and in `docs/client-integration.md`. The text may change; the code may not.
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
against the Compose stack and written down in `docs/development.md`. Do not add a test project.

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
  `docs/`: `deployment.md`, `configuration.md`, `authentication.md`, `operations.md`,
  `client-integration.md`, `development.md`. A subject gets a page when it is a subject, not because
  there is more to say about one that already has one.
- `docs/client-integration.md` is the contract with the client. Every endpoint, header, error code
  and behaviour a client depends on is described there, and a change to any of them changes that page
  in the same commit.
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

---

## Verified facts

Measured behaviour goes here, as the client's `CLAUDE.md` does it: what was measured, against what,
and what it means for the code. These are test results, not assumptions. Do not re-derive them, and
correct this section if a measurement ever contradicts it.
