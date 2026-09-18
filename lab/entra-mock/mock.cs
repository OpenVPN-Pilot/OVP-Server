#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true

// A stand-in for the Microsoft identity platform, for testing mode entra without a tenant. It publishes
// discovery and signing keys at the paths login.microsoftonline.com uses and mints access tokens shaped
// like Entra's, with whatever claims a test asks for. It serves HTTPS with a certificate from an
// authority it creates at start and writes to the shared trust folder, so the server trusts it the
// ordinary way.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

const string Base = "https://entra-mock:9443";
const string Kid = "lab-key-1";
using RSA signingKey = RSA.Create(2048);
using RSA rogueKey = RSA.Create(2048);

X509Certificate2 tls = CreateServerCertificate("/lab/trust/entra-mock-ca.pem");
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(9443, listen => listen.UseHttps(tls)));
WebApplication app = builder.Build();

app.MapGet("/{tenant}/v2.0/.well-known/openid-configuration", (string tenant) => Results.Json(new Dictionary<string, object>
{
    ["issuer"] = $"{Base}/{tenant}/v2.0",
    ["jwks_uri"] = $"{Base}/{tenant}/discovery/v2.0/keys",
    ["authorization_endpoint"] = $"{Base}/{tenant}/oauth2/v2.0/authorize",
    ["token_endpoint"] = $"{Base}/{tenant}/oauth2/v2.0/token",
    ["response_types_supported"] = new[] { "code" },
    ["subject_types_supported"] = new[] { "pairwise" },
    ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
}));

app.MapGet("/{tenant}/discovery/v2.0/keys", (string tenant) =>
{
    RSAParameters p = signingKey.ExportParameters(false);
    return Results.Json(new { keys = new[] { new { kty = "RSA", use = "sig", kid = Kid, n = B64(p.Modulus!), e = B64(p.Exponent!) } } });
});

// GET /mint?tenant=&aud=&azp=&scp=&roles=a,b&groups=g&oid=&upn=&name=&exp=<minutes>&ver=1|2&rogue=1&noscp=1&iss=
app.MapGet("/mint", (HttpRequest request) =>
{
    string Q(string name, string fallback) =>
        request.Query.TryGetValue(name, out var v) && v.Count > 0 && !string.IsNullOrEmpty(v[0]) ? v[0]! : fallback;

    string tenant = Q("tenant", "00000000-0000-0000-0000-000000000000");
    bool v1 = Q("ver", "2") == "1";
    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    Dictionary<string, object> claims = new()
    {
        ["aud"] = Q("aud", "api://00000000-0000-0000-0000-000000000000"),
        ["iss"] = Q("iss", v1 ? $"https://sts.windows.net/{tenant}/" : $"{Base}/{tenant}/v2.0"),
        ["iat"] = now - 10,
        ["nbf"] = now - 10,
        ["exp"] = now + (int.Parse(Q("exp", "60"), System.Globalization.CultureInfo.InvariantCulture) * 60),
        ["tid"] = tenant,
        ["oid"] = Q("oid", Guid.NewGuid().ToString()),
        ["scp"] = Q("scp", "access_as_user"),
        ["ver"] = v1 ? "1.0" : "2.0",
        ["name"] = Q("name", "Lab Person"),
    };
    AddIfSet(claims, v1 ? "upn" : "preferred_username", Q("upn", ""));
    AddIfSet(claims, v1 ? "appid" : "azp", Q("azp", ""));
    AddListIfSet(claims, "roles", Q("roles", ""));
    AddListIfSet(claims, "groups", Q("groups", ""));
    if (Q("noscp", "0") == "1")
    {
        claims.Remove("scp");
    }

    string header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = Kid }));
    string payload = B64(JsonSerializer.SerializeToUtf8Bytes(claims));
    RSA signer = Q("rogue", "0") == "1" ? rogueKey : signingKey;
    byte[] signature = signer.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return Results.Text(header + "." + payload + "." + B64(signature));
});

app.Run();

static void AddIfSet(Dictionary<string, object> claims, string name, string value)
{
    if (value.Length > 0)
    {
        claims[name] = value;
    }
}

static void AddListIfSet(Dictionary<string, object> claims, string name, string value)
{
    if (value.Length > 0)
    {
        claims[name] = value.Split(',');
    }
}

static string B64(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

static X509Certificate2 CreateServerCertificate(string authorityPath)
{
    DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
    DateTimeOffset until = DateTimeOffset.UtcNow.AddDays(30);

    using RSA authorityKey = RSA.Create(2048);
    CertificateRequest authorityRequest = new("CN=OVP Lab Entra Mock CA", authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
    authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
    using X509Certificate2 authority = authorityRequest.CreateSelfSigned(from, until);

    using RSA serverKey = RSA.Create(2048);
    CertificateRequest serverRequest = new("CN=entra-mock", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    SubjectAlternativeNameBuilder names = new();
    names.AddDnsName("entra-mock");
    serverRequest.CertificateExtensions.Add(names.Build());
    serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
    serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    using X509Certificate2 issued = serverRequest.Create(authority, from, until, RandomNumberGenerator.GetBytes(8));

    File.WriteAllText(authorityPath, authority.ExportCertificatePem());
    using X509Certificate2 withKey = issued.CopyWithPrivateKey(serverKey);
    return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
}
