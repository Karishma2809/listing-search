export type ListingStatus = 'active' | 'pending' | 'sold';

/** A listing as the API returns it, already ranked. */
export interface Listing {
  id: string;
  source: string;
  address: string;
  city: string;
  state: string;
  zip: string;
  price: number;
  bedrooms: number;
  bathrooms: number;
  squareFeet: number;
  listedDate: string;
  status: ListingStatus;
  description: string;
  /** 0-100, two decimals. */
  relevanceScore: number;
}

export interface SearchPage {
  items: Listing[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}
