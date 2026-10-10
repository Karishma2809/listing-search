import type { SearchPage } from '../domain/listing';
import { toQueryString, type FieldErrors, type SearchRequest } from '../domain/searchFilters';

/** A failed call, with the structured detail the API gave us (if it gave any). */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly fieldErrors: FieldErrors = {},
    readonly suggestions: string[] = [],
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/** The port the UI depends on; tests substitute a fake. */
export interface ListingApi {
  search(request: SearchRequest, signal?: AbortSignal): Promise<SearchPage>;
  cities(signal?: AbortSignal): Promise<string[]>;
}

const BASE = import.meta.env.VITE_API_BASE ?? '';

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${BASE}${path}`, { signal, headers: { Accept: 'application/json' } });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new ApiError('Could not reach the server. Is the API running?', 0);
  }

  if (response.ok) return (await response.json()) as T;
  throw await toApiError(response);
}

/** Maps an RFC 7807 problem response (or anything else a proxy might return) to an ApiError. */
async function toApiError(response: Response): Promise<ApiError> {
  try {
    const problem = (await response.json()) as {
      title?: string;
      errors?: FieldErrors;
      suggestions?: string[];
    };
    return new ApiError(
      problem.title ?? `The server rejected the request (${response.status}).`,
      response.status,
      problem.errors ?? {},
      problem.suggestions ?? [],
    );
  } catch {
    return new ApiError(`The server returned an unexpected response (${response.status}).`, response.status);
  }
}

export const listingApi: ListingApi = {
  search: (request, signal) => get<SearchPage>(`/api/listings/search?${toQueryString(request)}`, signal),
  cities: (signal) => get<string[]>('/api/cities', signal),
};
