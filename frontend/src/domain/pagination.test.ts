import { describe, expect, it } from 'vitest';
import { pageWindow, visibleRange } from './pagination';

describe('pageWindow', () => {
  it('shows nothing when there are no pages', () => {
    expect(pageWindow(1, 0)).toEqual([]);
  });

  it('shows a single page', () => {
    expect(pageWindow(1, 1)).toEqual([1]);
  });

  it('lists every page when there are few', () => {
    expect(pageWindow(2, 4)).toEqual([1, 2, 3, 4]);
  });

  it('collapses distant pages into a gap on both sides', () => {
    expect(pageWindow(10, 20)).toEqual([1, 'gap', 9, 10, 11, 'gap', 20]);
  });

  it('keeps the first page contiguous near the start', () => {
    expect(pageWindow(1, 20)).toEqual([1, 2, 'gap', 20]);
  });

  it('keeps the last page contiguous near the end', () => {
    expect(pageWindow(20, 20)).toEqual([1, 'gap', 19, 20]);
  });

  it('fills a one-page hole instead of showing a gap marker for it', () => {
    expect(pageWindow(4, 10)).toEqual([1, 2, 3, 4, 5, 'gap', 10]);
  });

  it('never repeats a page', () => {
    for (let total = 1; total <= 12; total++) {
      for (let current = 1; current <= total; current++) {
        const numbers = pageWindow(current, total).filter((item): item is number => item !== 'gap');
        expect(new Set(numbers).size).toBe(numbers.length);
        expect(numbers).toContain(current);
      }
    }
  });
});

describe('visibleRange', () => {
  it('describes a full middle page', () => {
    expect(visibleRange(2, 5, 5, 12)).toEqual({ from: 6, to: 10 });
  });

  it('describes a partial last page', () => {
    expect(visibleRange(3, 5, 2, 12)).toEqual({ from: 11, to: 12 });
  });

  it('is 0-0 when there is nothing to show', () => {
    expect(visibleRange(1, 5, 0, 0)).toEqual({ from: 0, to: 0 });
  });
});
