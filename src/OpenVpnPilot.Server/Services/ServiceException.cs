using OpenVpnPilot.Server.Contracts;

namespace OpenVpnPilot.Server.Services;

// A refusal a client can act on. The exception middleware turns it into problem details, so services
// state what went wrong once and controllers stay free of error handling.
public sealed class ServiceException(int status, string code, string detail, bool wipe = false) : Exception(detail)
{
    public int Status { get; } = status;

    public string Code { get; } = code;

    public bool Wipe { get; } = wipe;

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ServiceException BadRequest(string code, string detail) => new(StatusCodes.Status400BadRequest, code, detail);

    public static ServiceException Invalid(string field, string message) =>
        new(StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, message)
        {
            Errors = new Dictionary<string, string[]> { [field] = [message] },
        };

    public static ServiceException Unauthorized(string code, string detail) => new(StatusCodes.Status401Unauthorized, code, detail);

    public static ServiceException Revoked(string detail) =>
        new(StatusCodes.Status401Unauthorized, ErrorCodes.AccountRevoked, detail, wipe: true);

    public static ServiceException Forbidden(string code, string detail) => new(StatusCodes.Status403Forbidden, code, detail);

    public static ServiceException NotFound(string code, string detail) => new(StatusCodes.Status404NotFound, code, detail);

    public static ServiceException Conflict(string code, string detail) => new(StatusCodes.Status409Conflict, code, detail);

    public static ServiceException Gone(string code, string detail) => new(StatusCodes.Status410Gone, code, detail);

    public static ServiceException PreconditionFailed(string detail) =>
        new(StatusCodes.Status412PreconditionFailed, ErrorCodes.PreconditionFailed, detail);

    public static ServiceException Unavailable(string code, string detail) =>
        new(StatusCodes.Status503ServiceUnavailable, code, detail);
}
