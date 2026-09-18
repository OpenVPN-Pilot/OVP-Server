using System.Globalization;
using Novell.Directory.Ldap;
using OpenVpnPilot.Server.Configuration;

namespace OpenVpnPilot.Server.Auth.Ldap;

public sealed record LdapUserEntry(string Dn, string? DisplayName, bool Disabled);

public sealed class LdapDirectory(AuthOptions auth)
{
    // Active Directory's LDAP_MATCHING_RULE_IN_CHAIN: membership through nested groups counts too.
    private const string InChain = "1.2.840.113556.1.4.1941";

    // userAccountControl bit ACCOUNTDISABLE.
    private const int AccountDisabled = 0x2;

    private readonly LdapOptions options = auth.Ldap ?? throw new InvalidOperationException("LDAP options are missing.");

    public async Task<LdapUserEntry?> FindUserAsync(LdapConnection connection, string username)
    {
        string[] attributes = [options.DisplayNameAttribute, "userAccountControl"];
        ILdapSearchResults results = await connection.SearchAsync(
            options.BaseDn, LdapConnection.ScopeSub, LdapFilter.ForUser(options.UserFilter, username), attributes, false);

        List<LdapEntry> entries = [];
        while (await results.HasMoreAsync())
        {
            entries.Add(await results.NextAsync());
        }

        // More than one match means the filter is ambiguous, and guessing would sign in the wrong person.
        if (entries.Count != 1)
        {
            return null;
        }

        LdapEntry entry = entries[0];
        return new LdapUserEntry(entry.Dn, Read(entry, options.DisplayNameAttribute), IsDisabled(entry));
    }

    public async Task<bool> IsMemberAsync(LdapConnection connection, string userDn, string groupDn)
    {
        (string searchBase, string filter) = options.ActiveDirectory
            ? (userDn, $"(memberOf:{InChain}:={LdapFilter.Escape(groupDn)})")
            : (groupDn, $"(|(member={LdapFilter.Escape(userDn)})(uniqueMember={LdapFilter.Escape(userDn)}))");

        ILdapSearchResults results = await connection.SearchAsync(searchBase, LdapConnection.ScopeBase, filter, ["dn"], false);
        bool found = false;
        while (await results.HasMoreAsync())
        {
            try
            {
                await results.NextAsync();
                found = true;
            }
            catch (LdapException exception) when (exception.ResultCode == LdapException.NoSuchObject)
            {
                // A group that does not exist has no members; the configuration check reports it.
                return false;
            }
        }

        return found;
    }

    private bool IsDisabled(LdapEntry entry)
    {
        if (!options.ActiveDirectory)
        {
            return false;
        }

        string? flags = Read(entry, "userAccountControl");
        return flags is not null
            && int.TryParse(flags, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            && (value & AccountDisabled) != 0;
    }

    private static string? Read(LdapEntry entry, string attribute)
    {
        LdapAttributeSet set = entry.GetAttributeSet();
        return set.TryGetValue(attribute, out LdapAttribute? value) ? value.StringValue : null;
    }
}
