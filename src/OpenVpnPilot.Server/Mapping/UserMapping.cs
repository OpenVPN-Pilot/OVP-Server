using OpenVpnPilot.Server.Contracts.Responses;
using OpenVpnPilot.Server.Data.Entities;

namespace OpenVpnPilot.Server.Mapping;

public static class UserMapping
{
    public static CurrentUserResponse ToCurrentUser(this User user) =>
        new(user.Id, user.Username, user.DisplayName, user.Role.ToWire(), user.Provider.ToWire());

    public static UserResponse ToResponse(this User user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role.ToWire(),
            user.Provider.ToWire(),
            user.State.ToWire(),
            user.StateSource?.ToWire(),
            user.StateChangedAt,
            user.CreatedAt,
            user.LastLoginAt,
            user.LastSeenAt);

    // Enumerations go on the wire in lower case, the way the rest of the API spells its values.
    public static string ToWire<T>(this T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
}
