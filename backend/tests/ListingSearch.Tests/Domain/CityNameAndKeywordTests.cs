using ListingSearch.Domain.Listings;
using ListingSearch.Domain.Search;

namespace ListingSearch.Tests.Domain;

public class CityNameTests
{
    [Fact]
    public void Equality_ignores_case_and_stray_whitespace()
    {
        Assert.Equal(new CityName("Springfield"), new CityName("  springfield "));
        Assert.Equal(new CityName("New York"), new CityName("NEW   YORK"));
        Assert.NotEqual(new CityName("Springfield"), new CityName("Fairfax"));
    }

    [Fact]
    public void Display_keeps_a_readable_spelling_with_collapsed_whitespace() =>
        Assert.Equal("New York", new CityName(" New   York ").Display);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_user_input_means_no_city_filter(string? raw)
    {
        var result = CityName.FromUserInput(raw);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Absurdly_long_city_is_rejected()
    {
        var result = CityName.FromUserInput(new string('x', CityName.MaxLength + 1));

        Assert.Equal("city", Assert.Single(result.Errors).Field);
    }
}

public class SearchTermsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Blank_keyword_means_no_keyword_filter(string? raw)
    {
        var result = SearchTerms.FromUserInput(raw);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Keyword_is_split_lower_cased_and_de_duplicated()
    {
        var terms = SearchTerms.FromUserInput("Fenced  YARD, fenced!").Value!;

        Assert.Equal(["fenced", "yard"], terms.Words);
    }

    [Fact]
    public void Keyword_with_no_letters_or_digits_is_an_error_not_a_match_all()
    {
        var result = SearchTerms.FromUserInput("!!! ???");

        Assert.Equal("keyword", Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void Overlong_keyword_is_an_error()
    {
        var result = SearchTerms.FromUserInput(new string('a', SearchTerms.MaxLength + 1));

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("pet", "Pet friendly.", true)]
    [InlineData("pet", "Pets allowed.", true)]
    [InlineData("pet", "New carpet throughout.", false)]
    [InlineData("walk", "Walkable to Metro.", true)]
    [InlineData("kitchen yard", "Updated kitchen, fenced yard.", true)]
    [InlineData("kitchen pool", "Updated kitchen, fenced yard.", false)]
    public void Keyword_matches_the_start_of_whole_words_and_needs_every_term(string keyword, string description, bool expected)
    {
        var terms = SearchTerms.FromUserInput(keyword).Value!;

        Assert.Equal(expected, new ListingDescription(description).ContainsAllTerms(terms.Words));
    }
}
