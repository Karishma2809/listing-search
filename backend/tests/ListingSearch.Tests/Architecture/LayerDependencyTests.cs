using System.Reflection;
using ListingSearch.Application.Searching;
using ListingSearch.Domain.Listings;
using ListingSearch.Infrastructure.Persistence;

namespace ListingSearch.Tests.Architecture;

/// <summary>
/// The dependency rule, enforced: source-code dependencies point inward only.
///   Domain  &lt;-  Application  &lt;-  Infrastructure, Api
/// The project references already make most violations uncompilable; these tests also catch a
/// framework package sneaking into the inner layers.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Listing).Assembly;
    private static readonly Assembly Application = typeof(SearchListingsHandler).Assembly;
    private static readonly Assembly Infrastructure = typeof(InMemoryListingRepository).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static string[] References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToArray();

    [Fact]
    public void Domain_depends_on_no_other_layer_and_no_framework_beyond_the_base_library()
    {
        var references = References(Domain);

        Assert.DoesNotContain(references, name => name.StartsWith("ListingSearch", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("System.Text.Json", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_depends_only_on_the_domain()
    {
        var references = References(Application).Where(name => name.StartsWith("ListingSearch", StringComparison.Ordinal));

        Assert.Equal(["ListingSearch.Domain"], references);
        Assert.DoesNotContain(References(Application), name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_depends_inward_but_never_on_the_api()
    {
        var references = References(Infrastructure);

        Assert.Contains("ListingSearch.Domain", references);
        Assert.DoesNotContain("ListingSearch.Api", references);
    }

    [Fact]
    public void Api_is_the_only_layer_that_knows_infrastructure_by_name()
    {
        Assert.DoesNotContain("ListingSearch.Infrastructure", References(Domain));
        Assert.DoesNotContain("ListingSearch.Infrastructure", References(Application));
        Assert.Contains("ListingSearch.Infrastructure", References(Api));
    }
}
