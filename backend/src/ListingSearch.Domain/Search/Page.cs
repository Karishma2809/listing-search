using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Search;

/// <summary>One page of an ordered result set, plus enough context to navigate to its neighbours.</summary>
public sealed class Page<T>
{
    private Page(IReadOnlyList<T> items, int number, int size, int totalCount)
    {
        Items = items;
        Number = number;
        Size = size;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Number { get; }

    public int Size { get; }

    public int TotalCount { get; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Size);

    public bool HasPrevious => Number > 1;

    public bool HasNext => Number < TotalPages;

    /// <summary>
    /// Slices <paramref name="ordered"/>. A page past the end is an error, not an empty page: an empty
    /// page is indistinguishable from "no results" and hides a stale link. The first page of an empty
    /// result set is valid.
    /// </summary>
    public static Result<Page<T>> Create(IReadOnlyList<T> ordered, PageRequest request)
    {
        var totalCount = ordered.Count;
        var lastPage = Math.Max(1, (int)Math.Ceiling(totalCount / (double)request.Size));

        if (request.Number > lastPage)
        {
            var detail = totalCount == 0
                ? "there are no results, so only page 1 exists"
                : $"there {(lastPage == 1 ? "is 1 page" : $"are {lastPage} pages")} of results";
            return Result<Page<T>>.Failure("page", $"page {request.Number} is out of range: {detail}.");
        }

        var items = ordered.Skip((request.Number - 1) * request.Size).Take(request.Size).ToList();
        return Result<Page<T>>.Success(new Page<T>(items, request.Number, request.Size, totalCount));
    }
}
