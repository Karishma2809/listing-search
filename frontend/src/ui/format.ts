const USD = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });

export const formatPrice = (amount: number) => USD.format(amount);

/** Formats an ISO date (yyyy-mm-dd) without letting the viewer's time zone shift it by a day. */
export function formatDate(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number);
  if (!year || !month || !day) return iso;
  return new Date(year, month - 1, day).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export const formatBathrooms = (count: number) => (Number.isInteger(count) ? String(count) : count.toFixed(1));

export const formatScore = (score: number) => score.toFixed(2);
