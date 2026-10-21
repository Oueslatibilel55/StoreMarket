import { DestroyRef, inject, Injectable } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { TranslocoService } from '@jsverse/transloco';
import { combineLatest, Observable, of, Subscription, take } from 'rxjs';

/**
 * Page title and meta description, for browser tabs, shared links and search engines.
 * Translations load asynchronously: keys are resolved with `selectTranslate`, which waits for them.
 */
@Injectable({ providedIn: 'root' })
export class Seo {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly transloco = inject(TranslocoService);
  private pending: Subscription | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.pending?.unsubscribe());
  }

  /** A title typed by users (product, store name) — empty for the home page. */
  set(title: string, description?: string | null): void {
    this.apply(of(title), description ? of(description) : null);
  }

  /** A title that is a translation key (`shop.stores`). */
  setKey(titleKey: string): void {
    this.apply(this.transloco.selectTranslate<string>(titleKey), null);
  }

  private apply(title$: Observable<string>, description$: Observable<string> | null): void {
    this.pending?.unsubscribe();
    this.pending = combineLatest([
      title$,
      this.transloco.selectTranslate<string>('app.name'),
      description$ ?? this.transloco.selectTranslate<string>('app.tagline'),
    ]).pipe(take(1)).subscribe(([page, app, description]) => {
      this.title.setTitle(page ? `${page} · ${app}` : app);
      const text = (description || '').replace(/\s+/g, ' ').trim().slice(0, 160);
      this.meta.updateTag({ name: 'description', content: text });
      this.meta.updateTag({ property: 'og:title', content: page || app });
      this.meta.updateTag({ property: 'og:description', content: text });
    });
  }
}
