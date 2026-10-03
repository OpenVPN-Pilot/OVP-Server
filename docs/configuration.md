# Configuring OpenVPN Pilot Server

Everything is set through environment variables, which Compose reads from `.env`; `.env.example` lists
every one of them, apart from `OVP_DB_HOST`, which Compose sets itself. The server checks all of them
at start and, when something is wrong, stops with one line per problem naming the variable, rather than
failing on the first request that needs it. An empty value counts as unset. Switches take `true`, `1`,
`yes` or `on` and `false`, `0`, `no` or `off`, in any case, and a number outside its range is an error,
not a clamp.

Every variable can also be given as `<NAME>_FILE`, the path of a file that holds the value, which is
how Docker and Compose secrets arrive: `OVP_DATA_KEY_FILE=/run/secrets/ovp_data_key`. A value in the
environment is visible to anyone who may inspect the container; a mounted secret is not. Use it at
least for `OVP_DATA_KEY`, `OVP_JWT_SIGNING_KEY`, `OVP_DB_PASSWORD` and `OVP_LDAP_BIND_PASSWORD`. When
both are given, the file wins; a file that cannot be read stops the start.

Only the variables of the chosen sign in mode are read: the LDAP variables mean nothing in mode
`file`, and a mistake in them is not reported there.

## Database

| Variable | Default | |
| --- | --- | --- |
| `OVP_DB_PASSWORD` | required | Also creates the database user in the `postgres` container on its first start |
| `OVP_DB_HOST` | `postgres` | Set by Compose, which overrides a value in `.env`; not in `.env.example` for that reason |
| `OVP_DB_PORT` | `5432` | 1 to 65535 |
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
| `OVP_HTTPS_PORT` | `8443` | Listener in mode `kestrel`, 1 to 65535 |
| `OVP_HTTP_PORT` | `8080` | Listener in mode `proxy`, 1 to 65535 |
| `OVP_TLS_CERT_PATH` | required in `kestrel` | PEM certificate, or `.pfx`/`.p12`, told apart by the file extension |
| `OVP_TLS_KEY_PATH` | required for PEM | PEM private key |
| `OVP_TLS_CERT_PASSWORD` | | Password of a `.pfx` or of an encrypted PEM key |
| `OVP_TRUSTED_PROXIES` | required in `proxy` | Comma separated addresses or CIDR networks whose `X-Forwarded-For` and `X-Forwarded-Proto` headers are believed |

## Authentication

| Variable | Default | |
| --- | --- | --- |
| `OVP_AUTH_MODE` | required | `none`, `file`, `ldap` or `entra`, see [authentication.md](authentication.md) |
| `OVP_LOGIN_ATTEMPTS_PER_MINUTE` | `10` | Sign in and Entra exchange calls per client address and minute, 1 to 1000. Refreshes are limited separately, to 30 a minute per installation, so a team behind one address is not throttled |
| `OVP_AUTH_NONE_ADMINS` | | Mode `none`: comma separated user names that are administrators |
| `OVP_AUTH_FILE` | `/app/config/users.yaml` | Mode `file`: the user list. It must exist at start |

### LDAP and Active Directory

| Variable | Default | |
| --- | --- | --- |
| `OVP_LDAP_HOST` | required | Host name as it appears in the directory's certificate |
| `OVP_LDAP_SECURITY` | `ldaps` | `ldaps`, or `starttls` on the plain port. Unencrypted LDAP is not offered |
| `OVP_LDAP_PORT` | `636` or `389` | Follows `OVP_LDAP_SECURITY` |
| `OVP_LDAP_CA_CERT_PATH` | | PEM of the authority that issued the directory's certificate, when the system does not trust it. Read at start; a file that is missing or is not a certificate stops the server |
| `OVP_LDAP_BIND_DN` | required | Service account that searches for users |
| `OVP_LDAP_BIND_PASSWORD` | required | Its password |
| `OVP_LDAP_BASE_DN` | required | Where users are searched |
| `OVP_LDAP_ACTIVE_DIRECTORY` | `true` | Active Directory: disabled accounts are recognised and nested groups count |
| `OVP_LDAP_USER_FILTER` | see below | Must contain `{0}` for the escaped user name |
| `OVP_LDAP_DISPLAY_NAME_ATTRIBUTE` | `displayName` or `cn` | `displayName` with Active Directory, `cn` otherwise |
| `OVP_LDAP_ADMIN_GROUP` | required | DN of the group whose members are administrators. A DN the directory does not have answers sign in with 503 and an error naming this variable |
| `OVP_LDAP_USER_GROUP` | | DN of the group whose members may sign in as users. Unset: everyone the filter finds may sign in. Administrators need not be in it |

The default filter is `(&(objectClass=user)(sAMAccountName={0}))` for Active Directory and
`(&(objectClass=inetOrgPerson)(uid={0}))` otherwise.

### Entra ID

| Variable | Default | |
| --- | --- | --- |
| `OVP_ENTRA_INSTANCE` | `https://login.microsoftonline.com` | The identity platform of the tenant's cloud, for example `https://login.microsoftonline.us` for US Government. Must be https |
| `OVP_ENTRA_TENANT_ID` | required | Directory (tenant) id, a GUID. A domain name is refused at start, because the issuer of every token names the tenant by its id |
| `OVP_ENTRA_CLIENT_ID` | required | Application id of the registration clients sign in with |
| `OVP_ENTRA_AUDIENCE` | `api://<client id>` | Audience of the access tokens clients present. Both the `api://` form and the bare application id are accepted |
| `OVP_ENTRA_SCOPE` | `<audience>/access_as_user` | Scope clients request; the token must carry its last segment in `scp` |
| `OVP_ENTRA_ADMIN_ROLE` | `Admin` | App role value that makes an administrator |
| `OVP_ENTRA_USER_ROLE` | `User` | App role value that makes a user |
| `OVP_ENTRA_ADMIN_GROUP` | | Object id of a group whose members are administrators, as an alternative to the role |
| `OVP_ENTRA_USER_GROUP` | | Object id of a group whose members may sign in as users, as an alternative to the role. When set, nobody else may sign in |
| `OVP_ENTRA_REQUIRE_ROLE` | `false` | `true`: signing in needs a role or one of the two groups. With neither this nor `OVP_ENTRA_USER_GROUP`, everyone in the tenant is a user |
| `OVP_ENTRA_REAUTH_HOURS` | `8` | How long a session lasts before the client must sign in with Entra again, 1 to 720 |

## Clients

| Variable | Default | |
| --- | --- | --- |
| `OVP_MIN_CLIENT_VERSION` | `0.0.0` | Older clients are refused with `pilot.client_outdated`. A version with at least major and minor, such as `1.9.0` |
| `OVP_CLOCK_SKEW_SECONDS` | `300` | How far `X-Pilot-Timestamp` may be off the server's clock, 5 to 86400 |

## Swagger

| Variable | Default | |
| --- | --- | --- |
| `OVP_SWAGGER_ENABLED` | `true` | The interactive description at `/swagger` and the document at `/swagger/v1/swagger.json`. `false` removes both, and both then answer 404 |

## Logging

| Variable | Default | |
| --- | --- | --- |
| `OVP_LOG_LEVEL` | `Information` | `Verbose`, `Debug`, `Information`, `Warning`, `Error` or `Fatal`. `Debug` and below include the framework's own detail and every SQL statement |
| `OVP_LOG_DIRECTORY` | `/app/logs` | Mounted from `./logs` |
| `OVP_LOG_RETENTION_DAYS` | `7` | Day folders older than this are deleted, 1 to 3650 |

## Read by Compose

These are read by `docker-compose.yaml`; the server itself reads none of them, except that Compose passes
`TZ` on to the container.

| Variable | Default | |
| --- | --- | --- |
| `OVP_PUBLISH_PORT` | `8443` | Host port Compose publishes |
| `OVP_CONTAINER_PORT` | `8443` | Container port Compose publishes; `8080` in mode `proxy` |
| `TZ` | `UTC` | Time zone of log timestamps and of the day and hour a line is filed under |
