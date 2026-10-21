import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Public platform settings (GET /api/settings), loaded once. Values are invariant strings. */
@Injectable({ providedIn: 'root' })
export class PlatformSettings {
  private readonly values = toSignal(
    inject(HttpClient)
      .get<Record<string, string>>(`${environment.apiUrl}/settings`)
      .pipe(catchError(() => of({} as Record<string, string>)), shareReplay(1)),
    { initialValue: {} as Record<string, string> },
  );

  number(key: string, fallback: number): number {
    const value = Number(this.values()[key]);
    return Number.isFinite(value) && this.values()[key] !== undefined ? value : fallback;
  }
}
