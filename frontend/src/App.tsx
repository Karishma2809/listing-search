import { useEffect, useState } from 'react';
import { useListingSearch } from './application/useListingSearch';
import {
  DEFAULT_PAGE_SIZE,
  EMPTY_FILTERS,
  type FilterField,
  type SearchFilters,
  type SearchRequest,
} from './domain/searchFilters';
import { listingApi, type ListingApi } from './infrastructure/listingApi';
import { Pagination } from './ui/Pagination';
import { ResultsTable } from './ui/ResultsTable';
import { FORM_FIELD_NAMES, SearchForm } from './ui/SearchForm';

export function App({ api = listingApi }: { api?: ListingApi }) {
  const [draft, setDraft] = useState<SearchFilters>(EMPTY_FILTERS);
  const [request, setRequest] = useState<SearchRequest>({ filters: EMPTY_FILTERS, page: 1, pageSize: DEFAULT_PAGE_SIZE });
  const [cities, setCities] = useState<string[]>([]);
  const search = useListingSearch(request, api);

  // The city list only powers autocomplete, so failing to load it is not worth bothering the user about.
  useEffect(() => {
    const controller = new AbortController();
    api.cities(controller.signal).then(setCities, () => undefined);
    return () => controller.abort();
  }, [api]);

  const applyFilters = (filters: SearchFilters) => {
    setDraft(filters);
    setRequest((current) => ({ ...current, filters, page: 1 }));
  };

  const { status, data, error } = search;
  const loading = status === 'loading';
  const fieldErrors = error?.fieldErrors ?? {};
  // Errors for inputs on the form show beside the input; anything else (page, pageSize, ...) goes in the banner.
  const otherErrors = Object.entries(fieldErrors)
    .filter(([field]) => !FORM_FIELD_NAMES.includes(field))
    .flatMap(([, messages]) => messages);

  return (
    <main>
      <h1>Listing search</h1>

      <SearchForm
        values={draft}
        cities={cities}
        serverErrors={fieldErrors}
        busy={loading}
        onChange={(field: FilterField, value: string) => setDraft((current) => ({ ...current, [field]: value }))}
        onSubmit={() => applyFilters(draft)}
        onReset={() => applyFilters(EMPTY_FILTERS)}
      />

      <section className="results-area" aria-busy={loading}>
        {loading && (
          <p className="notice" role="status">
            Loading listings…
          </p>
        )}

        {status === 'error' && error && (
          <div className="notice error" role="alert">
            <strong>{error.message}</strong>
            {otherErrors.length > 0 && (
              <ul>
                {otherErrors.map((message) => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            )}
            {error.suggestions.length > 0 && (
              <p>
                Did you mean{' '}
                {error.suggestions.map((city) => (
                  <button key={city} type="button" className="link" onClick={() => applyFilters({ ...draft, city })}>
                    {city}
                  </button>
                ))}
                ?
              </p>
            )}
          </div>
        )}

        {status !== 'error' && data && data.totalCount === 0 && !loading && (
          <p className="notice" role="status">
            No listings match these filters. Try widening the price range or removing the keyword.
          </p>
        )}

        {status !== 'error' && data && data.items.length > 0 && (
          <div className={loading ? 'stale' : undefined}>
            <ResultsTable items={data.items} />
            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              shown={data.items.length}
              disabled={loading}
              onPageChange={(page) => setRequest((current) => ({ ...current, page }))}
              onPageSizeChange={(pageSize) => setRequest((current) => ({ ...current, pageSize, page: 1 }))}
            />
          </div>
        )}
      </section>
    </main>
  );
}
