using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace OpenVpnPilot.Server.Contracts.Requests;

// MVC validates the elements of a list but skips an element that is null, which then reaches the
// service as a value nobody checked.
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class NoNullItemsAttribute : ValidationAttribute
{
    public NoNullItemsAttribute()
        : base("The list must not contain null.")
    {
    }

    public override bool IsValid(object? value) => value is not IEnumerable items || items.Cast<object?>().All(item => item is not null);
}
