import type { Listing } from '../domain/listing';
import { formatBathrooms, formatDate, formatPrice, formatScore } from './format';

export function ResultsTable({ items }: { items: Listing[] }) {
  return (
    <table className="results">
      <caption className="sr-only">Matching listings, best match first</caption>
      <thead>
        <tr>
          <th scope="col">Address</th>
          <th scope="col" className="num">Price</th>
          <th scope="col" className="num">Beds</th>
          <th scope="col" className="num">Baths</th>
          <th scope="col">Listed</th>
          <th scope="col">Status</th>
          <th scope="col" className="num" title="How well the price fits your target budget, combined with how recently the listing went live.">
            Relevance
          </th>
        </tr>
      </thead>
      <tbody>
        {items.map((listing) => (
          <tr key={`${listing.source}/${listing.id}`}>
            <th scope="row">
              <span className="address">{listing.address}</span>
              <span className="muted">
                {listing.city}, {listing.state} {listing.zip} · {listing.source}
              </span>
              <span className="muted description">{listing.description}</span>
            </th>
            <td className="num" data-label="Price">{formatPrice(listing.price)}</td>
            <td className="num" data-label="Beds">{listing.bedrooms}</td>
            <td className="num" data-label="Baths">{formatBathrooms(listing.bathrooms)}</td>
            <td data-label="Listed">{formatDate(listing.listedDate)}</td>
            <td data-label="Status">
              <span className={`status status-${listing.status}`}>{listing.status}</span>
            </td>
            <td className="num score" data-label="Relevance">{formatScore(listing.relevanceScore)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
