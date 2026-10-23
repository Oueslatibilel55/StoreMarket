/** One CSV cell: numbers as is (dot decimals), text quoted, and formula-looking text neutralised. */
export function csvCell(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'number') return String(value);
  const text = /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
  return `"${text.replace(/"/g, '""')}"`;
}

/** A CSV Excel opens as is: ";" separators, CRLF, and a BOM so accents and Arabic survive. */
export function toCsv(headers: string[], rows: (string | number | null | undefined)[][]): string {
  return '﻿' + [headers, ...rows].map((r) => r.map(csvCell).join(';')).join('\r\n');
}

export function downloadCsv(content: string, name: string): void {
  const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}
