#!/bin/sh
# A throwaway Active Directory domain controller, CORP.EXAMPLE.COM, for testing mode ldap. Samba 4
# answers LDAP the way Windows Server does: sAMAccountName, userAccountControl, nested groups through
# LDAP_MATCHING_RULE_IN_CHAIN, and references to the other partitions on a search from the root.
set -e

# A file left from an earlier run would report the controller ready before it is.
rm -f /lab/trust/ad-ca.pem

if [ ! -f /var/lib/samba/private/sam.ldb ]; then
    rm -f /etc/samba/smb.conf
    samba-tool domain provision --realm=CORP.EXAMPLE.COM --domain=CORP --server-role=dc \
        --dns-backend=SAMBA_INTERNAL --adminpass='Admin-Pass-2026!' --host-name=dc1 >/dev/null
    PROVISIONED=1
fi

samba -D
sleep 5

if [ -n "$PROVISIONED" ]; then
    samba-tool user create svc-ovp 'Svc-Pass-2026!' >/dev/null
    samba-tool user create anna 'Anna-Pass-2026!' --given-name=Anna --surname=Example >/dev/null
    samba-tool user create ben 'Ben-Pass-2026!' --given-name=Ben --surname=Example >/dev/null
    samba-tool user create clara 'Clara-Pass-2026!' --given-name=Clara --surname=Example >/dev/null
    samba-tool group add "OVP Admins" >/dev/null
    samba-tool group add "OVP Users" >/dev/null
    samba-tool group add "Team A" >/dev/null
    samba-tool group addmembers "OVP Admins" anna >/dev/null
    samba-tool group addmembers "OVP Users" anna,"Team A" >/dev/null
    # ben is only in Team A, which is a member of OVP Users: access through a nested group.
    samba-tool group addmembers "Team A" ben >/dev/null
fi

# The authority Samba generated for its LDAPS certificate, for OVP_LDAP_CA_CERT_PATH.
cp /var/lib/samba/private/tls/ca.pem /lab/trust/ad-ca.pem
chmod 644 /lab/trust/ad-ca.pem
echo "Domain controller ready"
exec sleep infinity
