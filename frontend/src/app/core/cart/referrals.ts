import { Injectable } from '@angular/core';
import { normalizeCode } from './cart.service';

const STORAGE_KEY = 'webapp.referrals';
const KEEP_DAYS = 30;

interface Referral {
  code: string;
  at: number;
}

/**
 * Affiliate share links (`?ref=DRF-45`, 012) remember the code per store for 30 days, so a product of
 * that store added to the cart later still carries it. The cart checks it like any typed code.
 */
@Injectable({ providedIn: 'root' })
export class Referrals {
  remember(storeId: string, rawCode: string | null | undefined): void {
    const code = normalizeCode(rawCode);
    if (!code) return;
    const all = this.read();
    all[storeId] = { code, at: Date.now() };
    this.write(all);
  }

  codeFor(storeId: string): string | null {
    const referral = this.read()[storeId];
    return referral && Date.now() - referral.at < KEEP_DAYS * 86_400_000 ? referral.code : null;
  }

  private read(): Record<string, Referral> {
    try {
      return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}') ?? {};
    } catch {
      return {};
    }
  }

  private write(all: Record<string, Referral>): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(all));
    } catch {
      // Storage blocked: the code can still be typed in the cart.
    }
  }
}
