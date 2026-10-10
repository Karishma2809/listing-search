import { afterEach, describe, expect, it, vi } from 'vitest';
import { EMPTY_FILTERS } from '../domain/searchFilters';
import { ApiError, listingApi } from './listingApi';

const request = { filters: { ...EMPTY_FILTERS, city: 'Reston', minBedrooms: '3' }, page: 2, pageSize: 5 };

const respond = (body: unknown, status = 200, contentType = 'application/json') =>
  vi.fn().mockResolvedValue(
    new Response(typeof body === 'string' ? body : JSON.stringify(body), { status, headers: { 'Content-Type': contentType } }),
  );

afterEach(() => vi.unstubAllGlobals());

describe('listingApi.search', () => {
  it('calls the search endpoint with the request as a query string', async () => {
    const fetchMock = respond({ items: [], page: 2, pageSize: 5, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false });
    vi.stubGlobal('fetch', fetchMock);

    await listingApi.search(request);

    const url = new URL(fetchMock.mock.calls[0]![0] as string, 'http://x');
    expect(url.pathname).toBe('/api/listings/search');
    expect(url.searchParams.get('city')).toBe('Reston');
    expect(url.searchParams.get('minBedrooms')).toBe('3');
    expect(url.searchParams.get('page')).toBe('2');
    expect(url.searchParams.get('pageSize')).toBe('5');
  });

  it('returns the parsed page on success', async () => {
    vi.stubGlobal('fetch', respond({ items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false }));

    const page = await listingApi.search(request);

    expect(page.totalCount).toBe(0);
  });

  it('turns a validation problem into an ApiError carrying field errors', async () => {
    vi.stubGlobal(
      'fetch',
      respond({ title: 'The search request is invalid.', status: 400, errors: { pageSize: ['pageSize must be between 1 and 100.'] } }, 400, 'application/problem+json'),
    );

    const error = await listingApi.search(request).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      message: 'The search request is invalid.',
      fieldErrors: { pageSize: ['pageSize must be between 1 and 100.'] },
    });
  });

  it('carries city suggestions from a 404', async () => {
    vi.stubGlobal('fetch', respond({ title: 'City not found.', errors: { city: ['No listings exist for city.'] }, suggestions: ['Springfield'] }, 404));

    const error = (await listingApi.search(request).catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(404);
    expect(error.suggestions).toEqual(['Springfield']);
  });

  it('copes with a non-JSON error body such as a proxy error page', async () => {
    vi.stubGlobal('fetch', respond('<html>Bad gateway</html>', 502, 'text/html'));

    const error = (await listingApi.search(request).catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(502);
    expect(error.message).toContain('502');
    expect(error.fieldErrors).toEqual({});
  });

  it('reports an unreachable server as status 0 with a helpful message', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    const error = (await listingApi.search(request).catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(0);
    expect(error.message).toMatch(/could not reach the server/i);
  });

  it('lets an abort through untouched so callers can ignore it', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new DOMException('aborted', 'AbortError')));

    await expect(listingApi.search(request)).rejects.toMatchObject({ name: 'AbortError' });
  });
});

describe('listingApi.cities', () => {
  it('returns the city list', async () => {
    vi.stubGlobal('fetch', respond(['Fairfax', 'Reston']));

    await expect(listingApi.cities()).resolves.toEqual(['Fairfax', 'Reston']);
  });
});
