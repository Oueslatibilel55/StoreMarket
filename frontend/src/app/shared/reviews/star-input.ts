import { Component, input, model, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/** 1–5 star picker (017): a radio group, keyboard friendly. `<app-star-input [(value)]="stars" [name]="id" />`. */
@Component({
  selector: 'app-star-input',
  imports: [TranslocoPipe],
  template: `
    <fieldset class="inline-flex items-center gap-0.5" dir="ltr" (mouseleave)="hover.set(0)">
      <legend class="sr-only">{{ 'reviews.yourRating' | transloco }}</legend>
      @for (n of stars; track n) {
        <label class="cursor-pointer p-0.5 text-3xl leading-none transition-transform duration-150 hover:scale-110"
          [class]="n <= (hover() || value()) ? 'text-accent' : 'text-line-strong'" (mouseenter)="hover.set(n)">
          <input type="radio" class="peer sr-only" [name]="name()" [value]="n" [checked]="value() === n" (change)="value.set(n)" />
          <span aria-hidden="true" class="rounded peer-focus-visible:outline-2 peer-focus-visible:outline-brand">★</span>
          <span class="sr-only">{{ 'reviews.nStars' | transloco: { n } }}</span>
        </label>
      }
      @if (value()) {
        <span class="ms-2 text-sm font-medium text-ink-soft" dir="auto">{{ 'reviews.starWords.' + value() | transloco }}</span>
      }
    </fieldset>
  `,
})
export class StarInput {
  readonly value = model(0);
  readonly name = input.required<string>();
  protected readonly hover = signal(0);
  protected readonly stars = [1, 2, 3, 4, 5];
}
