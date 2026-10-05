# Working on OpenVPN Pilot Server

| Page | What it covers |
| --- | --- |
| [Architecture](architecture.md) | The layers, the request pipeline, how synchronised writes and secrets work |
| [Building and running](building.md) | Building, running the stack with a test certificate, changing the schema |
| [Testing](testing.md) | Proving a change against the running stack, and the test lab for LDAP, Active Directory and Entra ID |

The contract with clients is documented separately, in [client-integration](../client-integration/README.md),
and every change to it belongs in the same commit as the code.

## Contributing

Issues and pull requests are welcome. Work happens on `dev`; `master` only receives releases, and a
release is tagged `v<version>`.

- **Say why in the code.** Comments explain the reason, never the mechanism. The house rules are in
  `CLAUDE.md`, and it is the standard the code is held to.
- **Keep files short.** Around 200 lines, never more than 300, generated migrations aside.
- **Keep the contract.** An endpoint, header, error code or behaviour a client depends on changes the
  pages under `docs/client-integration/` in the same commit; an environment variable changes
  `.env.example` and [configuration.md](../configuration.md); anything a deployer or client developer
  notices gets a line in `CHANGELOG.md`.
- **No unit tests.** The reasons are in `CLAUDE.md`; [testing](testing.md) says how a change is proven
  instead.
- Commits are English, short, imperative and start with a gitmoji code, for example
  `:sparkles: add vault endpoints`.

## How this was built

The code was written with Claude Opus 5 working from the client's rules and code. `CLAUDE.md` is not a
prompt; it is the standard the code is held to. Judge it by the code.
