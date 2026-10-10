/** What the user typed into the form. Kept as text so a half-typed number is representable. */
export interface SearchFilters {
  minPrice: string;
  maxPrice: string;
  minBedrooms: string;
  city: string;
  keyword: string;
  targetBudget: string;
}

export type FilterField = keyof SearchFilters;

/** Messages keyed by the API's parameter name; the same shape the server's error responses use. */
export type FieldErrors = Record<string, string[]>;

export interface SearchRequest {
  filters: SearchFilters;
  page: number;
  pageSize: number;
}

export const EMPTY_FILTERS: SearchFilters = {
  minPrice: '',
  maxPrice: '',
  minBedrooms: '',
  city: '',
  keyword: '',
  targetBudget: '',
};

export const PAGE_SIZES = [5, 10, 20, 50] as const;
export const DEFAULT_PAGE_SIZE = 5;

const isBlank = (value: string) => value.trim() === '';

// Same grammar the API accepts: optional sign and decimal point, no exponent, hex or separators.
const DECIMAL = /^[+-]?(\d+\.?\d*|\.\d+)$/;
const INTEGER = /^[+-]?\d+$/;

const toNumber = (value: string): number => (DECIMAL.test(value.trim()) ? Number(value.trim()) : NaN);
const toInteger = (value: string): number => (INTEGER.test(value.trim()) ? Number(value.trim()) : NaN);

/**
 * Quick feedback before a request is made. The server re-checks everything and stays the authority;
 * this only spares the user a round trip for mistakes we can see locally.
 */
export function validateFilters(filters: SearchFilters): FieldErrors {
  const errors: FieldErrors = {};
  const add = (field: FilterField, message: string) => {
    (errors[field] ??= []).push(message);
  };

  const minPrice = toNumber(filters.minPrice);
  const maxPrice = toNumber(filters.maxPrice);

  if (!isBlank(filters.minPrice) && Number.isNaN(minPrice)) add('minPrice', 'Enter a number, e.g. 250000.');
  else if (minPrice < 0) add('minPrice', 'Must be zero or greater.');

  if (!isBlank(filters.maxPrice) && Number.isNaN(maxPrice)) add('maxPrice', 'Enter a number, e.g. 500000.');
  else if (maxPrice < 0) add('maxPrice', 'Must be zero or greater.');

  if (!Number.isNaN(minPrice) && !Number.isNaN(maxPrice) && minPrice > maxPrice) {
    add('minPrice', 'Minimum price cannot be greater than maximum price.');
  }

  if (!isBlank(filters.minBedrooms)) {
    const bedrooms = toInteger(filters.minBedrooms);
    if (Number.isNaN(bedrooms)) add('minBedrooms', 'Enter a whole number.');
    else if (bedrooms < 0) add('minBedrooms', 'Must be zero or greater.');
  }

  if (!isBlank(filters.targetBudget)) {
    const budget = toNumber(filters.targetBudget);
    if (Number.isNaN(budget)) add('targetBudget', 'Enter a number, e.g. 450000.');
    else if (budget <= 0) add('targetBudget', 'Must be greater than zero.');
  }

  return errors;
}

export const hasErrors = (errors: FieldErrors) => Object.keys(errors).length > 0;

/** Builds the API query string; blank filters are left out entirely. */
export function toQueryString({ filters, page, pageSize }: SearchRequest): string {
  const params = new URLSearchParams();
  for (const [name, value] of Object.entries(filters)) {
    if (!isBlank(value)) params.set(name, value.trim());
  }
  params.set('page', String(page));
  params.set('pageSize', String(pageSize));
  return params.toString();
}
