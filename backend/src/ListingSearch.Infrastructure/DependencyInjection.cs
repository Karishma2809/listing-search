using ListingSearch.Domain.Listings;
using ListingSearch.Infrastructure.Feeds;
using ListingSearch.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ListingSearch.Infrastructure;

public static class DependencyInjection
{
    public const string FeedPathKey = "Listings:FeedPath";
    private const string DefaultFeedPath = "Data/sample_listings.json";

    /// <summary>
    /// Registers the in-memory repository, filled from the configured JSON feed. The feed is read when
    /// the repository is first resolved; resolve it at startup to fail fast on a broken feed.
    /// </summary>
    public static IServiceCollection AddListingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IListingRepository>(provider =>
        {
            var configured = configuration[FeedPathKey] ?? DefaultFeedPath;
            var path = Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);

            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ListingFeed");
            var feed = JsonListingFeed.LoadFile(path);

            foreach (var rejection in feed.Rejections)
            {
                logger.LogWarning("Rejected feed listing - {Rejection}", rejection);
            }

            logger.LogInformation("Loaded {Count} listings from {Path} ({Rejected} rejected)", feed.Listings.Count, path, feed.Rejections.Count);
            return new InMemoryListingRepository(feed.Listings);
        });

        return services;
    }
}
