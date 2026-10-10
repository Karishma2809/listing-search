import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { App } from './App';
import type { Listing, SearchPage } from './domain/listing';
import type { SearchRequest } from './domain/searchFilters';
import { ApiError, type ListingApi } from './infrastructure/listingApi';

const listing = (id: string, overrides: Partial<Listing> = {}): Listing => ({
  id,
  source: 'MLS_A',
  address: `${id} Main St`,
  city: 'Springfield',
  state: 'VA',
  zip: '22150',
  price: 450_000,
  bedrooms: 3,
  bathrooms: 1.5,
  squareFeet: 1_000,
  listedDate: '2026-08-29',
  status: 'active',
  description: 'A fine home.',
  relevanceScore: 87.35,
  ...overrides,
});

const page = (items: Listing[], overrides: Partial<SearchPage> = {}): SearchPage => ({
  items,
  page: 1,
  pageSize: 5,
  totalCount: items.length,
  totalPages: items.length === 0 ? 0 : 1,
  hasNext: false,
  hasPrevious: false,
  ...overrides,
});

/** A fake API whose search behaviour each test supplies. */
const fakeApi = (search: (request: SearchRequest) => Promise<SearchPage>): ListingApi & { search: ReturnType<typeof vi.fn> } => ({
  search: vi.fn((request: SearchRequest) => search(request)),
  cities: vi.fn().mockResolvedValue(['Fairfax', 'Springfield']),
});

const lastRequest = (api: { search: ReturnType<typeof vi.fn> }) =>
  api.search.mock.calls.at(-1)![0] as SearchRequest;

describe('App', () => {
  it('searches on load and shows address, price, bedrooms and relevance for each result', async () => {
    const api = fakeApi(async () => page([listing('A1', { address: '123 Main St', price: 450_000, bedrooms: 2, relevanceScore: 91.5 })]));

    render(<App api={api} />);

    const row = (await screen.findByText('123 Main St')).closest('tr')!;
    expect(within(row).getByText('$450,000')).toBeInTheDocument();
    expect(within(row).getByText('2')).toBeInTheDocument();
    expect(within(row).getByText('91.50')).toBeInTheDocument();
    expect(lastRequest(api)).toMatchObject({ page: 1, pageSize: 5 });
  });

  it('shows a loading indicator while the request is in flight', async () => {
    let resolve!: (value: SearchPage) => void;
    const api = fakeApi(() => new Promise<SearchPage>((r) => (resolve = r)));

    render(<App api={api} />);

    expect(screen.getByText(/loading listings/i)).toBeInTheDocument();
    resolve(page([listing('A1')]));
    await screen.findByText('A1 Main St');
    expect(screen.queryByText(/loading listings/i)).not.toBeInTheDocument();
  });

  it('shows a no-results message, not an empty table, when nothing matches', async () => {
    const api = fakeApi(async () => page([]));

    render(<App api={api} />);

    expect(await screen.findByText(/no listings match these filters/i)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('sends the filters the user entered and returns to page 1', async () => {
    const user = userEvent.setup();
    const api = fakeApi(async () => page([listing('A1')]));
    render(<App api={api} />);
    await screen.findByText('A1 Main St');

    await user.type(screen.getByLabelText('Min price ($)'), '300000');
    await user.type(screen.getByLabelText('City'), 'Reston');
    await user.type(screen.getByLabelText('Keyword'), 'fenced yard');
    await user.type(screen.getByLabelText('Target budget ($)'), '450000');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() => expect(lastRequest(api).filters.city).toBe('Reston'));
    expect(lastRequest(api)).toMatchObject({
      page: 1,
      filters: { minPrice: '300000', keyword: 'fenced yard', targetBudget: '450000' },
    });
  });

  it('blocks an invalid form locally and explains why, without calling the API', async () => {
    const user = userEvent.setup();
    const api = fakeApi(async () => page([listing('A1')]));
    render(<App api={api} />);
    await screen.findByText('A1 Main St');
    const callsBefore = api.search.mock.calls.length;

    await user.type(screen.getByLabelText('Min price ($)'), '900000');
    await user.type(screen.getByLabelText('Max price ($)'), '100000');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    expect(await screen.findByText(/cannot be greater than maximum price/i)).toBeInTheDocument();
    expect(screen.getByLabelText('Min price ($)')).toHaveAttribute('aria-invalid', 'true');
    expect(api.search.mock.calls.length).toBe(callsBefore);
  });

  it('shows a server validation error beside the field it concerns and in an alert', async () => {
    const user = userEvent.setup();
    const api = fakeApi(async (request) => {
      if (request.filters.keyword) {
        throw new ApiError('The search request is invalid.', 400, { keyword: ['keyword must contain at least one letter or digit.'] });
      }
      return page([listing('A1')]);
    });
    render(<App api={api} />);
    await screen.findByText('A1 Main St');

    await user.type(screen.getByLabelText('Keyword'), '!!!');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The search request is invalid.');
    expect(screen.getByText(/must contain at least one letter or digit/i)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('offers city suggestions after an unknown city and applies one when clicked', async () => {
    const user = userEvent.setup();
    const api = fakeApi(async (request) => {
      if (request.filters.city === 'Sprngfield') {
        throw new ApiError('City not found.', 404, { city: ['No listings exist for city.'] }, ['Springfield']);
      }
      return page([listing('A1')]);
    });
    render(<App api={api} />);
    await screen.findByText('A1 Main St');

    await user.type(screen.getByLabelText('City'), 'Sprngfield');
    await user.click(screen.getByRole('button', { name: 'Search' }));
    await user.click(await screen.findByRole('button', { name: 'Springfield' }));

    await waitFor(() => expect(lastRequest(api).filters.city).toBe('Springfield'));
    expect(screen.getByLabelText('City')).toHaveValue('Springfield');
    expect(await screen.findByText('A1 Main St')).toBeInTheDocument();
  });

  it('shows a network failure as an alert', async () => {
    const api = fakeApi(async () => {
      throw new ApiError('Could not reach the server. Is the API running?', 0);
    });

    render(<App api={api} />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not reach the server/i);
  });

  it('pages forward and back, disabling Previous on the first page and Next on the last', async () => {
    const user = userEvent.setup();
    const pages: Record<number, SearchPage> = {
      1: page([listing('P1')], { page: 1, totalCount: 7, totalPages: 2, hasNext: true }),
      2: page([listing('P2')], { page: 2, totalCount: 7, totalPages: 2, hasPrevious: true }),
    };
    const api = fakeApi(async (request) => pages[request.page]!);
    render(<App api={api} />);

    await screen.findByText('P1 Main St');
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Page 1' })).toHaveAttribute('aria-current', 'page');

    await user.click(screen.getByRole('button', { name: 'Next' }));
    await screen.findByText('P2 Main St');
    expect(lastRequest(api).page).toBe(2);
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
    expect(screen.getByText(/showing 6–6 of 7 listings/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Previous' }));
    await screen.findByText('P1 Main St');
    expect(lastRequest(api).page).toBe(1);
  });

  it('jumps straight to a numbered page', async () => {
    const user = userEvent.setup();
    const api = fakeApi(async (request) =>
      page([listing(`P${request.page}`)], { page: request.page, totalCount: 15, totalPages: 3, hasNext: request.page < 3 }),
    );
    render(<App api={api} />);
    await screen.findByText('P1 Main St');

    await user.click(screen.getByRole('button', { name: 'Page 3' }));

    expect(await screen.findByText('P3 Main St')).toBeInTheDocument();
  });

  it('goes back to page 1 when the page size changes', async () => {
    const user = userEvent.setup();
    const api = fakeApi(async (request) =>
      page([listing('A1')], { page: request.page, pageSize: request.pageSize, totalCount: 30, totalPages: 6 }),
    );
    render(<App api={api} />);
    await screen.findByText('A1 Main St');
    await user.click(screen.getByRole('button', { name: 'Page 2' }));
    await waitFor(() => expect(lastRequest(api).page).toBe(2));

    await user.selectOptions(screen.getByLabelText(/per page/i), '20');

    await waitFor(() => expect(lastRequest(api)).toMatchObject({ page: 1, pageSize: 20 }));
  });

  it('clears the filters and searches again on Reset', async () => {
    const user = userEvent.setup();
    const api = fakeApi(async () => page([listing('A1')]));
    render(<App api={api} />);
    await screen.findByText('A1 Main St');
    await user.type(screen.getByLabelText('Keyword'), 'pool');
    await user.click(screen.getByRole('button', { name: 'Search' }));
    await waitFor(() => expect(lastRequest(api).filters.keyword).toBe('pool'));

    await user.click(screen.getByRole('button', { name: 'Reset' }));

    await waitFor(() => expect(lastRequest(api).filters.keyword).toBe(''));
    expect(screen.getByLabelText('Keyword')).toHaveValue('');
  });

  it('populates the city suggestions list from the API', async () => {
    const api = fakeApi(async () => page([listing('A1')]));
    const { container } = render(<App api={api} />);
    await screen.findByText('A1 Main St');

    await waitFor(() => expect(container.querySelectorAll('#city-options option')).toHaveLength(2));
  });

  it('still works when the city list cannot be loaded', async () => {
    const api = fakeApi(async () => page([listing('A1')]));
    api.cities = vi.fn().mockRejectedValue(new ApiError('down', 0));

    render(<App api={api} />);

    expect(await screen.findByText('A1 Main St')).toBeInTheDocument();
  });
});
