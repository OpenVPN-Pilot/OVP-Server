namespace OpenVpnPilot.Server.Contracts.Responses;

/// <summary>A profile without its configuration. Fetch the configuration separately when it is needed.</summary>
/// <param name="Id">Stable identifier, also the key of the profile's vault entries.</param>
/// <param name="Name">The name shown in the client.</param>
/// <param name="RemoteHost">The first <c>remote</c> of the configuration.</param>
/// <param name="RemotePort">Its port.</param>
/// <param name="Protocol"><c>udp</c> or <c>tcp</c>.</param>
/// <param name="RequiresCredentials">The configuration has <c>auth-user-pass</c>, so connecting asks for a user name and password.</param>
/// <param name="HasUnsupportedOptions">The configuration asks OpenVPN to run a program, which the client warns about.</param>
/// <param name="ProtectRoutes">Null follows the client setting.</param>
/// <param name="Notes">Free text.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</param>
/// <param name="Tags">Tag names.</param>
/// <param name="ContentHash">Lower case hex SHA-256 of the configuration, computed as the client computes it. A change means the configuration changed.</param>
/// <param name="ChangeSeq">The change number of the last change, the same numbers <c>GET /api/v1/sync/changes</c> counts in.</param>
/// <param name="ETag">Send in <c>If-Match</c> to update this version.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="CreatedBy">Who created it.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="UpdatedBy">Who changed it last.</param>
public sealed record ProfileResponse(
    Guid Id,
    string Name,
    string? RemoteHost,
    int? RemotePort,
    string? Protocol,
    bool RequiresCredentials,
    bool HasUnsupportedOptions,
    bool? ProtectRoutes,
    string? Notes,
    string? Colour,
    IReadOnlyList<string> Tags,
    string ContentHash,
    long ChangeSeq,
    string ETag,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);

/// <summary>A profile's configuration, what the client writes to disk to connect.</summary>
/// <param name="ProfileId">The profile.</param>
/// <param name="ContentHash">Lower case hex SHA-256 of <c>Configuration</c>.</param>
/// <param name="Configuration">The complete OpenVPN configuration with every certificate and key inline.</param>
public sealed record ProfileConfigurationResponse(
    Guid ProfileId,
    string ContentHash,
    string Configuration);

/// <summary>What happened to each profile of a batch.</summary>
/// <param name="Created">How many were created.</param>
/// <param name="Duplicates">How many had a configuration that is already stored or appears earlier in the batch.</param>
/// <param name="Rejected">How many were refused for another reason.</param>
/// <param name="Items">One result per item, in the order sent.</param>
public sealed record ProfileBatchResponse(
    int Created,
    int Duplicates,
    int Rejected,
    IReadOnlyList<ProfileBatchItemResponse> Items);

/// <summary>The outcome of one item of a batch.</summary>
/// <param name="Index">Position of the item in the request, from zero.</param>
/// <param name="Outcome"><c>created</c>, <c>duplicate</c> or <c>rejected</c>.</param>
/// <param name="Profile">The new profile, when created.</param>
/// <param name="Code">The error code, when not created.</param>
/// <param name="Detail">Why not, in words.</param>
public sealed record ProfileBatchItemResponse(
    int Index,
    string Outcome,
    ProfileResponse? Profile,
    string? Code,
    string? Detail);

/// <summary>A tag.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Name">Unique regardless of case.</param>
/// <param name="Colour">A colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</param>
/// <param name="ChangeSeq">The change number of the last change.</param>
public sealed record TagResponse(
    Guid Id,
    string Name,
    string? Colour,
    long ChangeSeq);
