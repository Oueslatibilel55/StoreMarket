import { Component, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';

/** The affiliate's code. Becoming an affiliate happens at sign-up or through a role request. */
@Component({
  selector: 'app-affiliate-card',
  imports: [TranslocoPipe],
  templateUrl: './affiliate-card.html',
})
export class AffiliateCard {
  protected readonly auth = inject(AuthService);
  protected readonly copied = signal(false);

  protected async copy(code: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(`#${code}`);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      // Clipboard blocked (permissions / insecure context): the code stays visible to copy by hand.
    }
  }
}
