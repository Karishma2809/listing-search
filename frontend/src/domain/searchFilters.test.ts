import { describe, expect, it } from 'vitest';
import { EMPTY_FILTERS, hasErrors, toQueryString, validateFilters, type SearchFilters } from './searchFilters';

const filters = (overrides: Partial<SearchFilters>): SearchFilters => ({ ...EMPTY_FILTERS, ...overrides });

describe('validateFilters', () => {
  it('accepts an empty form', () => {
    expect(hasErrors(validateFilters(EMPTY_FILTERS))).toBe(false);
  });

  it('accepts a fully filled, valid form', () => {
    const valid = filters({ minPrice: '100000', maxPrice: '500000.50', minBedrooms: '3', city: 'Reston', keyword: 'pool', targetBudget: '400000' });
    expect(validateFilters(valid)).toEqual({});
  });

  it('rejects min price above max price, and reports it against minPrice', () => {
    const errors = validateFilters(filters({ minPrice: '900', maxPrice: '100' }));
    expect(errors.minPrice).toEqual(['Minimum price cannot be greater than maximum price.']);
    expect(errors.maxPrice).toBeUndefined();
  });

  it('allows min price equal to max price', () => {
    expect(hasErrors(validateFilters(filters({ minPrice: '500', maxPrice: '500' })))).toBe(false);
  });

  it.each(['abc', '1e5', '0x10', '1,000', '$5', '--3'])('rejects %s as a price', (value) => {
    expect(validateFilters(filters({ minPrice: value })).minPrice).toBeDefined();
  });

  it('rejects negative numbers', () => {
    const errors = validateFilters(filters({ minPrice: '-1', maxPrice: '-5', minBedrooms: '-2' }));
    expect(Object.keys(errors).sort()).toEqual(['maxPrice', 'minBedrooms', 'minPrice']);
  });

  it.each(['2.5', '3.0', 'two'])('rejects %s as a bedroom count', (value) => {
    expect(validateFilters(filters({ minBedrooms: value })).minBedrooms).toBeDefined();
  });

  it('allows zero bedrooms', () => {
    expect(hasErrors(validateFilters(filters({ minBedrooms: '0' })))).toBe(false);
  });

  it.each(['0', '-10', 'lots'])('rejects %s as a target budget', (value) => {
    expect(validateFilters(filters({ targetBudget: value })).targetBudget).toBeDefined();
  });

  it('treats whitespace-only values as empty', () => {
    expect(hasErrors(validateFilters(filters({ minPrice: '   ', city: '  ' })))).toBe(false);
  });

  it('reports every bad field at once', () => {
    const errors = validateFilters(filters({ minPrice: 'x', minBedrooms: 'y', targetBudget: 'z' }));
    expect(Object.keys(errors).sort()).toEqual(['minBedrooms', 'minPrice', 'targetBudget']);
  });
});

describe('toQueryString', () => {
  it('always carries page and pageSize and omits blank filters', () => {
    expect(toQueryString({ filters: EMPTY_FILTERS, page: 1, pageSize: 5 })).toBe('page=1&pageSize=5');
  });

  it('includes supplied filters, trimmed and encoded', () => {
    const query = toQueryString({
      filters: filters({ city: '  New York ', keyword: 'fenced yard', minPrice: '1000' }),
      page: 2,
      pageSize: 10,
    });
    const params = new URLSearchParams(query);
    expect(params.get('city')).toBe('New York');
    expect(params.get('keyword')).toBe('fenced yard');
    expect(params.get('minPrice')).toBe('1000');
    expect(params.get('page')).toBe('2');
    expect(params.has('maxPrice')).toBe(false);
  });
});
