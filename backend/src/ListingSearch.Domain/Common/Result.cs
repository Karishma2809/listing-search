namespace ListingSearch.Domain.Common;

/// <summary>A single, field-addressed reason a piece of user input was rejected.</summary>
public sealed record ValidationError(string Field, string Message);

/// <summary>
/// Outcome of building a domain object from untrusted input. Factories that take user input return
/// this (carrying every problem found, not just the first) instead of throwing, because bad input
/// is an expected event, not an exceptional one.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, IReadOnlyList<ValidationError> errors)
    {
        _value = value;
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsSuccess => Errors.Count == 0;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value. Check IsSuccess first.");

    public static Result<T> Success(T value) => new(value, []);

    public static Result<T> Failure(IEnumerable<ValidationError> errors)
    {
        var list = errors.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("A failure needs at least one error.", nameof(errors));
        }

        return new Result<T>(default, list);
    }

    public static Result<T> Failure(string field, string message) =>
        Failure([new ValidationError(field, message)]);
}
