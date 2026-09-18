namespace OpenVpnPilot.Server.Configuration;

public sealed class ConfigurationException(IReadOnlyList<string> problems)
    : Exception("The environment is not a working configuration:" + Environment.NewLine
        + string.Join(Environment.NewLine, problems.Select(p => "  - " + p)))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}
