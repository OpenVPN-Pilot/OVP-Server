# Testing

There are no unit tests, on purpose; the reasons are in `CLAUDE.md`. A change is proven against the
running stack. See [the overview](README.md) for the other development pages.

`none` and `file` need nothing more than Swagger or `curl`. For `ldap` and `entra`, `lab/` adds two
stand-in identity providers to the same Compose project:

```bash
docker compose -f docker-compose.yaml -f lab/docker-compose.yaml up -d --build
```

The lab is for tests only: fixed passwords, a privileged container, authorities that live for a month.

## What to check

For every change to behaviour a client sees, check the error code and the log line as well as the happy
path, and update the pages under `docs/client-integration/`. The cases that have broken before are the
ones that need two things at once: a refresh with the same token twice, a synchronisation while someone
writes, a vault entry deleted and added again between two synchronisations, a cursor older than the
pruned deletions, and a body over the size limit.

A real client is the strongest test. OpenVPN Pilot's own repository runs its synchronisation, sign in,
offline behaviour and the wipe directive against this stack in its integration tests, with a checkout of
this repository beside it. Those tests live in the client's repository, not here.

## Active Directory

**`ad`** is a Samba 4 domain controller for `CORP.EXAMPLE.COM`, provisioned on first start, which answers
LDAP the way Windows Server does. Its users and groups:

| User | Password | Groups |
| --- | --- | --- |
| `anna` | `Anna-Pass-2026!` | OVP Admins, OVP Users |
| `ben` | `Ben-Pass-2026!` | Team A, which is a member of OVP Users |
| `clara` | `Clara-Pass-2026!` | none |
| `svc-ovp` | `Svc-Pass-2026!` | the service account |

```
OVP_AUTH_MODE=ldap
OVP_LDAP_HOST=dc1.corp.example.com
OVP_LDAP_CA_CERT_PATH=/lab/trust/ad-ca.pem
OVP_LDAP_BIND_DN=CN=svc-ovp,CN=Users,DC=corp,DC=example,DC=com
OVP_LDAP_BIND_PASSWORD=Svc-Pass-2026!
OVP_LDAP_BASE_DN=DC=corp,DC=example,DC=com
OVP_LDAP_ADMIN_GROUP=CN=OVP Admins,CN=Users,DC=corp,DC=example,DC=com
OVP_LDAP_USER_GROUP=CN=OVP Users,CN=Users,DC=corp,DC=example,DC=com
```

Change the directory with `docker compose exec ad samba-tool ...`, for example `user disable ben` or
`group removemembers "OVP Users" "Team A"`, and watch the next refresh.

## Entra ID

**`entra-mock`** publishes discovery and signing keys at Entra's paths and mints tokens shaped like
Entra's with whatever claims a test needs. It creates its own certificate authority at start and puts
it into the lab's trust folder, which the API trusts in the lab through `SSL_CERT_DIR`; restart the API
after restarting the mock, because the folder is read when the process starts.

```
OVP_AUTH_MODE=entra
OVP_ENTRA_INSTANCE=https://entra-mock:9443
OVP_ENTRA_TENANT_ID=aaaaaaaa-0000-0000-0000-000000000001
OVP_ENTRA_CLIENT_ID=bbbbbbbb-0000-0000-0000-000000000002
```

```bash
docker compose -f docker-compose.yaml -f lab/docker-compose.yaml exec entra-mock curl -sk \
  "https://localhost:9443/mint?tenant=aaaaaaaa-0000-0000-0000-000000000001&aud=api://bbbbbbbb-0000-0000-0000-000000000002&roles=Admin&upn=admin@example.com"
```

The token goes to `POST /api/v1/auth/entra/exchange`. Pass the same `oid` every time a test signs in as
the same person, as Entra would: without it the mock invents one, and the server then refuses the known
name as belonging to someone else. Pass `azp` with the client id, as Entra does; a token without one is
accepted, a token with another one is refused. `/mint` also takes:

| Parameter | Effect |
| --- | --- |
| `scp` | The scope claim; default `access_as_user` |
| `noscp=1` | No scope claim at all |
| `roles`, `groups` | Comma separated app roles and group object ids |
| `name` | The display name |
| `exp` | Lifetime in minutes, negative for an expired token |
| `ver=1` | A version 1 token, with `upn` and `appid` instead of `preferred_username` and `azp` |
| `rogue=1` | A signature by a key the mock does not publish |
| `iss` | Claim another issuer |

A real Entra ID tenant has not been tried; everything above is the stand-in.
