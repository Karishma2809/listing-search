import { pageWindow, visibleRange } from '../domain/pagination';
import { PAGE_SIZES } from '../domain/searchFilters';

interface Props {
  page: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  shown: number;
  disabled: boolean;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

export function Pagination({ page, pageSize, totalPages, totalCount, shown, disabled, onPageChange, onPageSizeChange }: Props) {
  const { from, to } = visibleRange(page, pageSize, shown, totalCount);

  return (
    <div className="pagination">
      <p className="muted" role="status">
        Showing {from}–{to} of {totalCount} {totalCount === 1 ? 'listing' : 'listings'}
      </p>

      <nav aria-label="Result pages">
        <button type="button" onClick={() => onPageChange(page - 1)} disabled={disabled || page <= 1}>
          Previous
        </button>
        {pageWindow(page, totalPages).map((item, index) =>
          item === 'gap' ? (
            <span key={`gap-${index}`} className="gap" aria-hidden="true">
              …
            </span>
          ) : (
            <button
              key={item}
              type="button"
              className={item === page ? 'current' : 'secondary'}
              aria-current={item === page ? 'page' : undefined}
              aria-label={`Page ${item}`}
              onClick={() => onPageChange(item)}
              disabled={disabled}
            >
              {item}
            </button>
          ),
        )}
        <button type="button" onClick={() => onPageChange(page + 1)} disabled={disabled || page >= totalPages}>
          Next
        </button>
      </nav>

      <label className="page-size">
        Per page{' '}
        <select value={pageSize} onChange={(event) => onPageSizeChange(Number(event.target.value))} disabled={disabled}>
          {PAGE_SIZES.map((size) => (
            <option key={size} value={size}>
              {size}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}
