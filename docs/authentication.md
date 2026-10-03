# Signing in to OpenVPN Pilot Server

`OVP_AUTH_MODE` chooses one of four ways, and all four end the same way: the server issues its own
short lived access token and a refresh token, and checks the account again on every refresh. Roles,
revocation and the wipe directive therefore behave identically whichever mode is chosen.

There are two roles. **Administrators** change the shared profiles, tags and vault and manage users.
**Users** read everything, connect, add vault entries that are missing and keep their own
favourites, shortcuts and settings. The role always comes from the identity provider.

## `none`: a name only

Anyone who can reach the server signs in with any name and no password, including the names of the
administrators, and the server says so in its log at every start. Meant for a closed network where the
server is a shared library, not a gate; never for a server reachable from anywhere else.
Administrators are the names in `OVP_AUTH_NONE_ADMINS`; everybody else is a user.

A name that an administrator deleted cannot sign in again unless it is enabled or purged.

## `file`: a user list

`./config/users.yaml`, mounted at `/app/config/users.yaml`:

```yaml
users:
  - username: alice
    displayName: Alice Example
    role: admin
    password: "$argon2id$v=19$m=65536,t=3,p=1$...$..."

  - username: bob
    role: user
    password: "$argon2id$v=19$m=65536,t=3,p=1$...$..."
    disabled: false
```

User names are compared regardless of case. `role` is `admin` or `user` and defaults to `user`; a user
listed twice, or with any other role, is refused like any other broken file, see below.

Create a hash without the password ending up in the shell history:

```bash
docker compose run --rm api hash-password
```

A plain password in the file works too, and the server warns about each one at every start.

The server reads the file again within a few seconds of a change, without a restart: it looks at the
modification time at most every five seconds and reads a changed file once it was last written two
seconds ago, so a file caught halfway through being saved is not taken for the new list. Removing a
user or setting `disabled: true` takes effect on that user's next request: their clients are told to
erase everything they received from this server.

A file that no longer parses, lists no users at all, or has a user without a name or password is
ignored with an error in the log, and the previous list stays in force; at start it stops the server
instead. An emptied file is far more often an accident than a decision, and taking it at its word would
tell every client to erase itself.

## `ldap`: LDAP or Active Directory

The server binds with a service account, finds the user with `OVP_LDAP_USER_FILTER`, and proves the
password by binding as that user. An empty password is refused before anything is sent, because most
directories accept it as an anonymous bind. Only LDAPS or StartTLS is used; a directory with a private
certificate authority is trusted by naming that authority in `OVP_LDAP_CA_CERT_PATH`.

- Members of `OVP_LDAP_ADMIN_GROUP` are administrators.
- With `OVP_LDAP_USER_GROUP` set, only its members (and administrators) may sign in at all.
- With `OVP_LDAP_ACTIVE_DIRECTORY=true`, nested group membership counts and a disabled account
  (`userAccountControl`) cannot sign in. Other directories need `groupOfNames` or
  `groupOfUniqueNames` groups.

Active Directory example:

```
OVP_AUTH_MODE=ldap
OVP_LDAP_HOST=dc01.corp.example.com
OVP_LDAP_BIND_DN=CN=svc-ovp,OU=Service Accounts,DC=corp,DC=example,DC=com
OVP_LDAP_BIND_PASSWORD=...
OVP_LDAP_BASE_DN=DC=corp,DC=example,DC=com
OVP_LDAP_ADMIN_GROUP=CN=OVP Admins,OU=Groups,DC=corp,DC=example,DC=com
OVP_LDAP_USER_GROUP=CN=OVP Users,OU=Groups,DC=corp,DC=example,DC=com
```

Both group DNs are looked up in the directory before membership is decided. A group that does not exist,
usually a typing error in the variable, answers sign in and refresh with 503 `auth.provider_unavailable`
and an error in the log that names the variable: every one when it is `OVP_LDAP_ADMIN_GROUP`, which is
asked first, and those of users who are not administrators when it is `OVP_LDAP_USER_GROUP`. Nobody is
locked out or wiped over it. A user filter that matches more than one entry for a name signs nobody in
under that name and, on a refresh, likewise answers 503 instead of treating the account as removed.

The directory is asked again on every refresh, so a user who is deleted, disabled or removed from the
user group loses access within one access token lifetime and their clients are told to erase what
they hold. When the directory cannot be reached, sign in and refresh answer
`auth.provider_unavailable` and clients keep working from what they have.

What Active Directory needs from its side:

- **A certificate on the domain controllers.** A domain controller offers LDAPS and StartTLS only once
  it has a server certificate, usually from Active Directory Certificate Services with the Domain
  Controller template. Without one, port 636 refuses the handshake. `OVP_LDAP_HOST` must be a name in
  that certificate, and `OVP_LDAP_CA_CERT_PATH` the issuing authority exported as PEM.
- **A service account** that may read users and groups. An ordinary domain user can.
- A disabled account cannot sign in and is answered like a wrong password, since a disabled account's
  password cannot be checked. Sessions it already has end with the wipe directive at their next refresh.
  A client whose refresh token has already expired therefore never receives the wipe directive for an
  account disabled in Active Directory; disable the user on the server as well when that matters.

Searching from the domain root, Active Directory also answers with references to its other partitions;
they are skipped, as they hold no users of the domain. This mode has been measured against a Samba 4
domain controller, which answers LDAP the way Windows Server does, including nested groups; the setup is
in `lab/` and described in [testing](development/testing.md#active-directory).

## `entra`: Entra ID

Clients sign in with Microsoft themselves and hand the server the resulting access token, which the
server checks against Entra's published keys. The server needs no secret of its own.

One app registration is enough. It is both the API clients sign in to and the public client they sign
in as, so there is no client secret anywhere. In the Microsoft Entra admin center
(`entra.microsoft.com`), as someone who may register applications:

1. **Register the application.** *Identity, Applications, App registrations, New registration.* Any name,
   for example `OpenVPN Pilot`. *Supported account types:* accounts in this organisational directory
   only. Leave the redirect URI empty and register. On the **Overview** page note the
   *Application (client) id* and the *Directory (tenant) id*: they become `OVP_ENTRA_CLIENT_ID` and
   `OVP_ENTRA_TENANT_ID`.
2. **Let the desktop client sign in.** *Authentication, Add a platform, Mobile and desktop
   applications*, custom redirect URI `http://localhost`, which is what the client's system browser
   sign in returns to. Under *Advanced settings* set **Allow public client flows** to *Yes* and save.
3. **Expose the API.** *Expose an API, Application ID URI, Add*, and keep the proposed
   `api://<application id>`. Then *Add a scope*: name `access_as_user`, *Who can consent:* admins and
   users, any display names and descriptions, state enabled. A different URI or scope name works too,
   but then set `OVP_ENTRA_AUDIENCE` and `OVP_ENTRA_SCOPE` to match.
4. **Allow the client to request that scope.** *API permissions, Add a permission, APIs my
   organization uses* (or *My APIs*), pick this application, delegated permission `access_as_user`,
   add, then **Grant admin consent**. Without the consent every user is asked to consent at their first
   sign in, or refused when users may not consent to applications in the tenant.
5. **Create the roles.** *App roles, Create app role*, twice: display name and **value** `Admin`, then
   `User`, *Allowed member types:* users/groups, enabled. The value is what the server compares,
   case sensitive (`OVP_ENTRA_ADMIN_ROLE`, `OVP_ENTRA_USER_ROLE`).
6. **Assign people.** *Identity, Applications, Enterprise applications*, the same application, *Users
   and groups, Add user/group*: pick people or groups and a role. Assigning a group needs Entra ID P1 or
   higher; without it assign people one by one, or use groups by object id as below.
7. **Optionally keep everyone else out at Entra already.** In the same enterprise application,
   *Properties*, **Assignment required** *Yes*: then only assigned people can obtain a token at all.
   `OVP_ENTRA_REQUIRE_ROLE=true` enforces the same on the server's side.

Then:

```
OVP_AUTH_MODE=entra
OVP_ENTRA_TENANT_ID=<directory (tenant) id>
OVP_ENTRA_CLIENT_ID=<application (client) id>
OVP_ENTRA_REQUIRE_ROLE=true
```

and restart. `GET /api/v1/server/info` now answers `authMode: entra` with the tenant, client id, scope and
authority, which is everything a client needs; nobody types them into the client.

Who is what, in order:

| | By app role | Or by group (object id) |
| --- | --- | --- |
| Administrator | `OVP_ENTRA_ADMIN_ROLE`, default `Admin` | `OVP_ENTRA_ADMIN_GROUP` |
| User | `OVP_ENTRA_USER_ROLE`, default `User` | `OVP_ENTRA_USER_GROUP` |

Anyone else is refused when `OVP_ENTRA_USER_GROUP` is set or `OVP_ENTRA_REQUIRE_ROLE=true`, and is a
user otherwise. Groups need the group claim: *App registrations*, the application, *Token
configuration, Add groups claim*, **Security groups**, and for the access token the *Group ID*
format; the variables take the group's *Object id* from its overview page. A person in more than 200 groups receives no group claim at all (Entra's overage
rule), so for large directories assign the app roles to the groups instead; roles are always in the token.

Someone who has signed in before and later loses the role or group is treated like a disabled account:
their clients are told to erase what they hold at the next sign in with Entra.

Two more checks guard the exchange. The token must have been requested by `OVP_ENTRA_CLIENT_ID`
itself (its `azp` or `appid`), so another application in the tenant that was granted the scope cannot
sign people in here. People are recognised by their Entra object id, which never changes: a renamed
account keeps its record, and a name that reappears with a different object id is refused with
`auth.identity_conflict` rather than given the earlier person's record, until an administrator purges
the old one.

The server cannot ask Entra whether an account still exists without a permission it deliberately does
not have, so a session lasts `OVP_ENTRA_REAUTH_HOURS` and the client then signs in with Entra again.
That is when a disabled account is noticed. An administrator who needs someone gone at once disables
them in the server as well.

For a tenant in a national cloud, set `OVP_ENTRA_INSTANCE`, for example to
`https://login.microsoftonline.us`.

Measured: forged and expired tokens against Entra's real published keys, and the whole path against the
stand-in identity provider in `lab/`, which signs tokens the way Entra does. Accepted there: version 1
and 2 tokens, the audience as `api://` URI or as bare application id, the admin role, the user role, the user group, a renamed account and
the admin group. Refused: a wrong audience, a missing or different scope, an expired token, a forged
signature, another tenant's issuer, a token requested by another application, someone new with neither
role nor group, and a known name under a different object id; and a session past
`OVP_ENTRA_REAUTH_HOURS`. Signing in with a
token from a real tenant has not been measured.

## Switching modes

Users are recorded by name. When the mode changes, a session started under the previous mode is ended
at its next refresh with `auth.reauthentication_required`, and nothing is erased; the person signs in
again the new way, and a user of the same name keeps their favourites, shortcuts and settings.
