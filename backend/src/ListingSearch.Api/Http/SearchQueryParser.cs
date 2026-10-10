using System.Globalization;
using ListingSearch.Application.Searching;
using ListingSearch.Domain.Common;

namespace ListingSearch.Api.Http;

/// <summary>
/// HTTP adapter: turns query-string text into a typed <see cref="SearchListingsQuery"/>. It only checks
/// what HTTP can check (is this a number? was it sent twice? do we know this parameter?). What the
/// numbers mean is the domain's business and is validated there.
/// </summary>
internal static class SearchQueryParser
{
    internal static readonly string[] SupportedParameters =
        ["minPrice", "maxPrice", "minBedrooms", "city", "keyword", "targetBudget", "page", "pageSize"];

    public static SearchListingsQuery Parse(IQueryCollection query)
    {
        var errors = new List<ValidationError>();

        // A misspelt filter ("minBedroom") must not be silently ignored - the caller would get unfiltered results.
        foreach (var key in query.Keys.Where(key => !SupportedParameters.Contains(key, StringComparer.OrdinalIgnoreCase)))
        {
            errors.Add(new ValidationError(key, $"'{key}' is not a supported parameter. Supported: {string.Join(", ", SupportedParameters)}."));
        }

        var parsed = new SearchListingsQuery(
            MinPrice: Decimal(query, "minPrice", errors),
            MaxPrice: Decimal(query, "maxPrice", errors),
            MinBedrooms: Integer(query, "minBedrooms", errors),
            City: Text(query, "city", errors),
            Keyword: Text(query, "keyword", errors),
            TargetBudget: Decimal(query, "targetBudget", errors),
            Page: Integer(query, "page", errors),
            PageSize: Integer(query, "pageSize", errors));

        return errors.Count > 0 ? throw new InvalidSearchRequestException(errors) : parsed;
    }

    /// <summary>A missing or empty value means "not provided" (HTML forms send empty strings for blank inputs).</summary>
    private static string? Text(IQueryCollection query, string name, List<ValidationError> errors)
    {
        if (!query.TryGetValue(name, out var values))
        {
            return null;
        }

        if (values.Count > 1)
        {
            errors.Add(new ValidationError(name, $"{name} may be given only once."));
            return null;
        }

        return string.IsNullOrWhiteSpace(values[0]) ? null : values[0]!.Trim();
    }

    private static decimal? Decimal(IQueryCollection query, string name, List<ValidationError> errors)
    {
        var text = Text(query, name, errors);
        if (text is null)
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add(new ValidationError(name, $"{name} must be a number (got '{text}')."));
        return null;
    }

    private static int? Integer(IQueryCollection query, string name, List<ValidationError> errors)
    {
        var text = Text(query, name, errors);
        if (text is null)
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add(new ValidationError(name, $"{name} must be a whole number (got '{text}')."));
        return null;
    }
}
