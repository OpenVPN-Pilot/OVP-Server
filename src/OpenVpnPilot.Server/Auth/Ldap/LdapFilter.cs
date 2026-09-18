using System.Text;

namespace OpenVpnPilot.Server.Auth.Ldap;

public static class LdapFilter
{
    // RFC 4515. A name like 'a*)(uid=*' must match that literal name, not every entry in the directory.
    public static string Escape(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char c in value)
        {
            _ = c switch
            {
                '\\' => builder.Append(@"\5c"),
                '*' => builder.Append(@"\2a"),
                '(' => builder.Append(@"\28"),
                ')' => builder.Append(@"\29"),
                '\0' => builder.Append(@"\00"),
                _ => builder.Append(c),
            };
        }

        return builder.ToString();
    }

    public static string ForUser(string template, string username) =>
        template.Replace("{0}", Escape(username), StringComparison.Ordinal);
}
