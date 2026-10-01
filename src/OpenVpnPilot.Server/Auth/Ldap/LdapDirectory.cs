using System.Collections.Concurrent;
using System.Globalization;
using Novell.Directory.Ldap;
using OpenVpnPilot.Server.Configuration;

namespace OpenVpnPilot.Server.Auth.Ldap;

public sealed record LdapUserEntry(string Dn, string? DisplayName, bool Disabled);

public enum LdapMatch
{
    Found,
    NotFound,
    Ambiguous,
}

public sealed record LdapUserLookup(LdapMatch Match, LdapUserEntry? Entry, int Count);

// A group named in the configuration that the directory does not have. Membership of it cannot be
// decided, and answering "not a member" would lock out and wipe everyone who should be in it.
public sealed class LdapGroupMissingException(string variable, string groupDn)
    : Exception($"{variable} names '{groupDn}', which the directory does not have.")
{
    public string Variable { get; } = variable;

    public string GroupDn { get; } = groupDn;
}

public sealed class LdapDirectory(AuthOptions auth, ILogger<LdapDirectory> logger)
{
    // Active Directory's LDAP_MATCHING_RULE_IN_CHAIN: membership through nested groups counts too.
    private const string InChain = "1.2.840.113556.1.4.1941";

    // userAccountControl bit ACCOUNTDISABLE.
    private const int AccountDisabled = 0x2;

    private readonly LdapOptions options = auth.Ldap ?? throw new InvalidOperationException("LDAP options are missing.");

    // Groups only ever confirmed, never cached as missing, so a group created after a typo is fixed in
    // the directory is found on the next request.
    private readonly ConcurrentDictionary<string, bool> confirmedGroups = new(StringComparer.OrdinalIgnoreCase);

    public async Task<LdapUserLookup> FindUserAsync(LdapConnection connection, string username, CancellationToken cancellationToken)
    {
        string[] attributes = [options.DisplayNameAttribute, "userAccountControl"];
        ILdapSearchResults results = await connection.SearchAsync(
            options.BaseDn, LdapConnection.ScopeSub, LdapFilter.ForUser(options.UserFilter, username), attributes, false, cancellationToken);

        List<LdapEntry> entries = [];
        while (await results.HasMoreAsync(cancellationToken))
        {
            try
            {
                entries.Add(await results.NextAsync(cancellationToken));
            }
            catch (LdapReferralException)
            {
                // Active Directory answers a search from the domain root with references to its other
                // partitions (Configuration, DomainDnsZones, ForestDnsZones). None of them holds the
                // domain's users, and following them would mean binding to other servers.
                AuthLog.ReferralSkipped(logger, options.BaseDn);
            }
        }

        // More than one match means the filter is ambiguous, and guessing would sign in the wrong person.
        if (entries.Count != 1)
        {
            return new LdapUserLookup(entries.Count == 0 ? LdapMatch.NotFound : LdapMatch.Ambiguous, null, entries.Count);
        }

        LdapEntry entry = entries[0];
        return new LdapUserLookup(LdapMatch.Found, new LdapUserEntry(entry.Dn, Read(entry, options.DisplayNameAttribute), IsDisabled(entry)), 1);
    }

    public async Task<bool> IsMemberAsync(
        LdapConnection connection, string userDn, string groupDn, string variable, CancellationToken cancellationToken)
    {
        // Active Directory's memberOf filter matches nothing for a group that does not exist, without an
        // error, so existence is asked separately.
        await RequireGroupAsync(connection, groupDn, variable, cancellationToken);

        (string searchBase, string filter) = options.ActiveDirectory
            ? (userDn, $"(memberOf:{InChain}:={LdapFilter.Escape(groupDn)})")
            : (groupDn, $"(|(member={LdapFilter.Escape(userDn)})(uniqueMember={LdapFilter.Escape(userDn)}))");

        return await AnyAsync(connection, searchBase, filter, cancellationToken);
    }

    private async Task RequireGroupAsync(LdapConnection connection, string groupDn, string variable, CancellationToken cancellationToken)
    {
        if (confirmedGroups.ContainsKey(groupDn))
        {
            return;
        }

        try
        {
            if (!await AnyAsync(connection, groupDn, "(objectClass=*)", cancellationToken))
            {
                throw new LdapGroupMissingException(variable, groupDn);
            }
        }
        catch (LdapException exception) when (exception.ResultCode == LdapException.NoSuchObject)
        {
            throw new LdapGroupMissingException(variable, groupDn);
        }

        confirmedGroups[groupDn] = true;
    }

    private static async Task<bool> AnyAsync(LdapConnection connection, string searchBase, string filter, CancellationToken cancellationToken)
    {
        ILdapSearchResults results = await connection.SearchAsync(
            searchBase, LdapConnection.ScopeBase, filter, ["dn"], false, cancellationToken);
        bool found = false;
        while (await results.HasMoreAsync(cancellationToken))
        {
            await results.NextAsync(cancellationToken);
            found = true;
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
