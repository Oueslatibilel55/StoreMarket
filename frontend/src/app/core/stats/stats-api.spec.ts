import { toBars } from '../../shared/stats/daily-chart';
import { csvCell, toCsv } from '../../shared/csv';
import { dayBefore, percent, presetOf } from './stats-api';

const today = new Date(Date.UTC(2026, 9, 23, 15, 30));

describe('stats helpers', () => {
  it('counts days back in UTC', () => {
    expect(dayBefore(today, 0)).toBe('2026-10-23');
    expect(dayBefore(today, 29)).toBe('2026-09-24');
    expect(dayBefore(new Date(Date.UTC(2026, 2, 1)), 1)).toBe('2026-02-28');
  });

  it('recognises a preset period, else custom', () => {
    expect(presetOf({ from: '2026-09-24', to: '2026-10-23' }, today)).toBe(30);
    expect(presetOf({ from: '2026-10-17', to: '2026-10-23' }, today)).toBe(7);
    expect(presetOf({ from: '2026-10-01', to: '2026-10-23' }, today)).toBeNull();
    expect(presetOf({ from: '2026-09-23', to: '2026-10-22' }, today)).toBeNull(); // 30 days, not ending today
  });

  it('gives shares without dividing by zero', () => {
    expect(percent(1, 3)).toBe(33);
    expect(percent(2, 3)).toBe(67);
    expect(percent(5, 0)).toBe(0);
  });

  it('keeps one bar per day for short periods and adds up weeks for long ones', () => {
    const days = (n: number) =>
      Array.from({ length: n }, (_, i) => ({ date: dayBefore(today, n - 1 - i), placed: 1, delivered: 1, sales: 10 }));

    expect(toBars(days(30)).length).toBe(30);
    const weeks = toBars(days(100));
    expect(weeks.length).toBe(15); // 14 full weeks + 2 days
    expect(weeks[0]).toEqual(expect.objectContaining({ placed: 7, sales: 70, from: days(100)[0].date, to: days(100)[6].date }));
    expect(weeks[14].placed).toBe(2);
    expect(weeks.reduce((s, b) => s + b.sales, 0)).toBe(1000);
  });

  it('writes Excel-safe CSV', () => {
    expect(csvCell(null)).toBe('');
    expect(csvCell(12.5)).toBe('12.5');
    expect(csvCell('Crème "bio"')).toBe('"Crème ""bio"""');
    expect(csvCell('=HYPERLINK()')).toBe(`"'=HYPERLINK()"`);
    expect(toCsv(['a', 'b'], [[1, 'x']])).toBe('﻿"a";"b"\r\n1;"x"');
  });
});
