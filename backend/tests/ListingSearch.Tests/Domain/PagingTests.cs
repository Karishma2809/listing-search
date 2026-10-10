using ListingSearch.Domain.Search;

namespace ListingSearch.Tests.Domain;

public class PageRequestTests
{
    [Fact]
    public void Omitted_values_default_to_the_first_page_of_ten()
    {
        var request = PageRequest.Create(null, null).Value;

        Assert.Equal(1, request.Number);
        Assert.Equal(PageRequest.DefaultPageSize, request.Size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Page_below_one_is_rejected(int page)
    {
        var error = Assert.Single(PageRequest.Create(page, null).Errors);

        Assert.Equal("page", error.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(PageRequest.MaxPageSize + 1)]
    public void Page_size_outside_one_to_max_is_rejected_rather_than_silently_clamped(int pageSize)
    {
        var error = Assert.Single(PageRequest.Create(null, pageSize).Errors);

        Assert.Equal("pageSize", error.Field);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(PageRequest.MaxPageSize)]
    public void Page_size_bounds_are_inclusive(int pageSize) =>
        Assert.Equal(pageSize, PageRequest.Create(1, pageSize).Value.Size);

    [Fact]
    public void Both_problems_are_reported_together() =>
        Assert.Equal(2, PageRequest.Create(0, 0).Errors.Count);
}

public class PageTests
{
    private static IReadOnlyList<int> Numbers(int count) => Enumerable.Range(1, count).ToList();

    private static Page<int> Slice(int total, int page, int size) =>
        Page<int>.Create(Numbers(total), PageRequest.Create(page, size).Value).Value;

    [Fact]
    public void Middle_page_holds_exactly_one_page_of_the_right_items()
    {
        var page = Slice(total: 12, page: 2, size: 5);

        Assert.Equal([6, 7, 8, 9, 10], page.Items);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Fact]
    public void Last_page_may_be_partial()
    {
        var page = Slice(total: 12, page: 3, size: 5);

        Assert.Equal([11, 12], page.Items);
        Assert.False(page.HasNext);
        Assert.True(page.HasPrevious);
    }

    [Fact]
    public void Exact_multiple_of_page_size_has_no_trailing_empty_page()
    {
        var page = Slice(total: 10, page: 2, size: 5);

        Assert.Equal(2, page.TotalPages);
        Assert.Equal(5, page.Items.Count);
        Assert.False(page.HasNext);
    }

    [Fact]
    public void Page_size_larger_than_the_result_set_gives_one_page()
    {
        var page = Slice(total: 3, page: 1, size: 50);

        Assert.Equal(3, page.Items.Count);
        Assert.Equal(1, page.TotalPages);
        Assert.False(page.HasNext);
        Assert.False(page.HasPrevious);
    }

    [Fact]
    public void Empty_result_set_has_a_valid_empty_first_page()
    {
        var page = Slice(total: 0, page: 1, size: 10);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNext);
        Assert.False(page.HasPrevious);
    }

    [Fact]
    public void Page_past_the_end_is_an_error_naming_the_last_page()
    {
        var result = Page<int>.Create(Numbers(12), PageRequest.Create(4, 5).Value);

        var error = Assert.Single(result.Errors);
        Assert.Equal("page", error.Field);
        Assert.Contains("3 pages", error.Message);
    }

    [Fact]
    public void Page_two_of_an_empty_result_set_is_an_error() =>
        Assert.False(Page<int>.Create(Numbers(0), PageRequest.Create(2, 10).Value).IsSuccess);

    [Fact]
    public void Absurdly_large_page_number_is_an_error_not_an_overflow() =>
        Assert.False(Page<int>.Create(Numbers(5), PageRequest.Create(int.MaxValue, 100).Value).IsSuccess);
}
