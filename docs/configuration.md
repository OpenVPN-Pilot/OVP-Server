# Configuring OpenVPN Pilot Server

Everything is set through environment variables, which Compose reads from `.env`. The server checks
all of them at start and, when something is wrong, stops with one line per problem naming the
variable, rather than failing on the first request that needs it. An empty value counts as unset.

## Database

| Variable | Default | |
| --- | --- | --- |
| `OVP_DB_PASSWORD` | required | Also creates the database user in the `postgres` container on its first start |
| `OVP_DB_HOST` | `postgres` | Set by Compose |
| `OVP_DB_PORT` | `5432` | |
| `OVP_DB_NAME` | `ovp` | |
| `OVP_DB_USER` | `ovp` | |
| `OVP_DB_MIGRATE_ON_START` | `true` | Applies schema migrations at start. With `false` the schema must already match |

## Keys

| Variable | Default | |
| --- | --- | --- |
| `OVP_DATA_KEY` | required | Exactly 32 bytes, Base64. Encrypts profile configurations and vault entries. Losing it loses them |
| `OVP_JWT_SIGNING_KEY` | required | At least 32 bytes, Base64. Signs access tokens; changing it signs everyone out |
| `OVP_ACCESS_TOKEN_MINUTES` | `15` | Lifetime of an access token, 1 to 1440 |
| `OVP_REFRESH_TOKEN_DAYS` | `30` | Lifetime of a refresh token, 1 to 365. A client idle for longer signs in again |

## TLS

| Variable | Default | |
| --- | --- | --- |
| `OVP_TLS_MODE` | required | `kestrel` or `proxy`, see [deployment.md](deployment.md#tls) |
| `OVP_HTTPS_PORT` | `8443` | Listener in mode `kestrel` |
| `OVP_HTTP_PORT` | `8080` | Listener in mode `proxy` |
| `OVP_TLS_CERT_PATH` | required in `kestrel` | PEM certificate, or `.pfx`/`.p12` |
| `OVP_TLS_KEY_PATH` | required for PEM | PEM private key |
| `OVP_TLS_CERT_PASSWORD` | | Password of a `.pfx` or of an encrypted PEM key |
| `OVP_TRUSTED_PROXIES` | required in `proxy` | Comma separated addresses or CIDR networks whose `X-Forwarded-*` headers are believed |
| `OVP_PUBLISH_PORT` | `8443` | Host port, used by Compose only |
| `OVP_CONTAINER_PORT` | `8443` | Container port Compose publishes; `8080` in mode `proxy` |

## Authentication

| Variable | Default | |
| --- | --- | --- |
| `OVP_AUTH_MODE` | required | `none`, `file`, `ldap` or `entra`, see [authentication.md](authentication.md) |
| `OVP_LOGIN_ATTEMPTS_PER_MINUTE` | `10` | Sign in, refresh and Entra exchange calls per client address and minute |
| `OVP_AUTH_NONE_ADMINS` | | Mode `none`: comma separated user names that are administrators |
| `OVP_AUTH_FILE` | `/app/config/users.yaml` | Mode `file`: the user list |

### LDAP and Active Directory

| Variable | Default | |
| --- | --- | --- |
| `OVP_LDAP_HOST` | required | Host name as it appears in the directory's certificate |
| `OVP_LDAP_SECURITY` | `ldaps` | `ldaps`, or `starttls` on the plain port. Unencrypted LDAP is not offered |
| `OVP_LDAP_PORT` | `636` or `389` | Follows `OVP_LDAP_SECURITY` |
| `OVP_LDAP_CA_CERT_PATH` | | PEM of the authority that issued the directory's certificate, when the system does not trust it |
| `OVP_LDAP_BIND_DN` | required | Service account that searches for users |
| `OVP_LDAP_BIND_PASSWORD` | required | Its password |
| `OVP_LDAP_BASE_DN` | required | Where users are searched |
| `OVP_LDAP_ACTIVE_DIRECTORY` | `true` | Active Directory: disabled accounts are recognised and nested groups count |
| `OVP_LDAP_USER_FILTER` | see below | Must contain `{0}` for the escaped user name |
| `OVP_LDAP_DISPLAY_NAME_ATTRIBUTE` | `displayName` or `cn` | |
| `OVP_LDAP_ADMIN_GROUP` | required | DN of the group whose members are administrators |
| `OVP_LDAP_USER_GROUP` | | DN of the group whose members may sign in at all. Unset: everyone found by the filter |

The default filter is `(&(objectClass=user)(sAMAccountName={0}))` for Active Directory and
`(&(objectClass=inetOrgPerson)(uid={0}))` otherwise.

### Entra ID

| Variable | Default | |
| --- | --- | --- |
| `OVP_ENTRA_TENANT_ID` | required | Directory (tenant) id |
| `OVP_ENTRA_CLIENT_ID` | required | Application id of the registration clients sign in with |
| `OVP_ENTRA_AUDIENCE` | `api://<client id>` | Audience of the access tokens clients present |
| `OVP_ENTRA_SCOPE` | `<audience>/access_as_user` | Scope clients request; the token must carry its last segment in `scp` |
| `OVP_ENTRA_ADMIN_ROLE` | `Admin` | App role value that makes an administrator |
| `OVP_ENTRA_USER_ROLE` | `User` | App role value that makes a user |
| `OVP_ENTRA_ADMIN_GROUP` | | Object id of a group whose members are administrators, as an alternative to the role |
| `OVP_ENTRA_REQUIRE_ROLE` | `false` | `true`: signing in needs the user or admin role; otherwise everyone in the tenant is a user |
| `OVP_ENTRA_REAUTH_HOURS` | `8` | How long a session lasts before the client must sign in with Entra again |

## Clients

| Variable | Default | |
| --- | --- | --- |
| `OVP_MIN_CLIENT_VERSION` | `0.0.0` | Older clients are refused with `pilot.client_outdated` |
| `OVP_CLOCK_SKEW_SECONDS` | `300` | How far `X-Pilot-Timestamp` may be off the server's clock |

## Swagger

| Variable | Default | |
| --- | --- | --- |
| `OVP_SWAGGER_ENABLED` | `true` | The interactive description at `/swagger` and the document at `/swagger/v1/swagger.json`. `false` removes both |

## Logging

| Variable | Default | |
| --- | --- | --- |
| `OVP_LOG_LEVEL` | `Information` | `Verbose`, `Debug`, `Information`, `Warning`, `Error` or `Fatal`. `Debug` and below include the framework's own detail and every SQL statement |
| `OVP_LOG_DIRECTORY` | `/app/logs` | Mounted from `./logs` |
| `OVP_LOG_RETENTION_DAYS` | `7` | Day folders older than this are deleted |
| `TZ` | `UTC` | Time zone of log timestamps and of the day and hour a line is filed under |
