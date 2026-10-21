import { HttpErrorResponse } from '@angular/common/http';

/**
 * Translation key for an API error. The API sends RFC 9457 ProblemDetails with a `code`
 * (e.g. "auth.emailTaken"), translated under `errors.*` in public/i18n.
 */
export function errorKey(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'errors.network';
    if (error.status === 429) return 'errors.tooManyRequests';
    const code = (error.error as { code?: unknown } | null)?.code;
    if (typeof code === 'string' && code.length > 0) return `errors.${code}`;
  }
  return 'errors.unknown';
}
