using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace OpenVpnPilot.Server.Security;

// Reads and writes the PHC string format, $argon2id$v=19$m=65536,t=3,p=1$salt$hash, which is what
// other tools print as well, so a hash made elsewhere works here.
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "$argon2id$";
    private const int MemoryKib = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public bool IsHash(string stored) => stored.StartsWith(Prefix, StringComparison.Ordinal);

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Compute(password, salt, MemoryKib, Iterations, Parallelism, HashBytes);
        return string.Create(CultureInfo.InvariantCulture,
            $"{Prefix}v=19$m={MemoryKib},t={Iterations},p={Parallelism}${Encode(salt)}${Encode(hash)}");
    }

    public bool Verify(string password, string stored)
    {
        if (!IsHash(stored))
        {
            // A plain password in the user file. Hashing both sides keeps the comparison constant in time.
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(password)),
                SHA256.HashData(Encoding.UTF8.GetBytes(stored)));
        }

        string[] parts = stored.Split('$');
        if (parts.Length != 6 || !TryReadParameters(parts[3], out int memory, out int iterations, out int parallelism))
        {
            return false;
        }

        byte[] salt = Decode(parts[4]);
        byte[] expected = Decode(parts[5]);
        byte[] actual = Compute(password, salt, memory, iterations, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Compute(string password, byte[] salt, int memory, int iterations, int parallelism, int length)
    {
        using Argon2id argon = new(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memory,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon.GetBytes(length);
    }

    private static bool TryReadParameters(string text, out int memory, out int iterations, out int parallelism)
    {
        memory = iterations = parallelism = 0;
        foreach (string pair in text.Split(','))
        {
            string[] kv = pair.Split('=');
            if (kv.Length != 2 || !int.TryParse(kv[1], NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                return false;
            }

            switch (kv[0])
            {
                case "m": memory = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
                default: return false;
            }
        }

        return memory > 0 && iterations > 0 && parallelism > 0;
    }

    private static string Encode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=');

    private static byte[] Decode(string text) =>
        Convert.FromBase64String(text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '='));
}
