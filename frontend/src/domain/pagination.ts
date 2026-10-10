export type PageItem = number | 'gap';

/**
 * Which page buttons to show: always the first and last page, plus a window around the current one,
 * with a gap marker wherever pages are skipped. Never lists a page twice and never a gap of one page.
 */
export function pageWindow(current: number, totalPages: number, radius = 1): PageItem[] {
  if (totalPages < 1) return [];

  const wanted = new Set<number>([1, totalPages]);
  for (let page = current - radius; page <= current + radius; page++) {
    if (page >= 1 && page <= totalPages) wanted.add(page);
  }

  const pages = [...wanted].sort((a, b) => a - b);
  const items: PageItem[] = [];
  pages.forEach((page, index) => {
    const previous = pages[index - 1];
    if (previous !== undefined) {
      if (page - previous === 2) items.push(previous + 1);
      else if (page - previous > 2) items.push('gap');
    }
    items.push(page);
  });
  return items;
}

/** "6-10" style bounds of the items shown on a page, for the "Showing x-y of z" caption. */
export function visibleRange(page: number, pageSize: number, shown: number, total: number) {
  if (total === 0 || shown === 0) return { from: 0, to: 0 };
  const from = (page - 1) * pageSize + 1;
  return { from, to: from + shown - 1 };
}
