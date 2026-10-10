namespace ListingSearch.Domain.Common;

/// <summary>
/// An invariant of an entity or value object was violated. Used for data that should never be
/// invalid (e.g. a malformed feed row), as opposed to user search input, which uses <see cref="Result{T}"/>.
/// </summary>
public sealed class DomainException(string message) : Exception(message);

internal static class Guard
{
    public static string NotBlank(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} must not be blank.");
        }

        return value.Trim();
    }

    public static void InRange(double value, double min, double max, string name)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            throw new DomainException($"{name} must be between {min} and {max} (was {value}).");
        }
    }
}
