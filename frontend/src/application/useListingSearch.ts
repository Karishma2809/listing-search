import { useEffect, useState } from 'react';
import type { SearchPage } from '../domain/listing';
import type { SearchRequest } from '../domain/searchFilters';
import { ApiError, type ListingApi } from '../infrastructure/listingApi';

export interface SearchState {
  status: 'loading' | 'success' | 'error';
  /** The most recent successful page; kept while the next one loads so the table does not blink. */
  data: SearchPage | null;
  error: ApiError | null;
}

/**
 * Runs a search whenever the request changes. A response that arrives after the request it answers
 * has been superseded is discarded (the old call is aborted and its result ignored).
 */
export function useListingSearch(request: SearchRequest, api: ListingApi): SearchState {
  const [state, setState] = useState<SearchState>({ status: 'loading', data: null, error: null });

  useEffect(() => {
    const controller = new AbortController();
    setState((previous) => ({ status: 'loading', data: previous.data, error: null }));

    api
      .search(request, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setState({ status: 'success', data, error: null });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const apiError = error instanceof ApiError ? error : new ApiError('Something went wrong while searching.', 0);
        setState((previous) => ({ status: 'error', data: previous.data, error: apiError }));
      });

    return () => controller.abort();
  }, [request, api]);

  return state;
}
