namespace OpenVpnPilot.Server.Data.Entities;

public enum UserRole
{
    User,
    Admin,
}

public enum UserState
{
    Active,
    Disabled,

    // Kept as a row rather than removed, so a client still holding a token is told to wipe itself
    // and so the same name cannot simply sign in again in a mode without passwords.
    Deleted,
}

public enum AuthProviderKind
{
    None,
    File,
    Ldap,
    Entra,
}

public enum StateSource
{
    // The identity provider reported the account as gone or disabled, and may report it back.
    Provider,

    // An administrator decided, and only an administrator reverses it.
    Administrator,
}
