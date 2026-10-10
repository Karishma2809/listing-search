using ListingSearch.Api.Composition;
using ListingSearch.Api.Http;
using ListingSearch.Domain.Listings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddListingSearch(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SearchExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapListingEndpoints();

// Fail fast: read the feed now, so a missing or corrupt feed stops startup rather than the first request.
_ = app.Services.GetRequiredService<IListingRepository>();

app.Run();

// Exposed so the integration tests can host the app in-process.
public partial class Program;
