import { HttpEventType } from '@angular/common/http';
import { Component, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../core/http/problem';
import { ACCEPTED_IMAGE_TYPES, ImagePurpose, ImagesApi, UploadedImage } from '../../core/images/images-api';
import { PlatformSettings } from '../../core/settings/platform-settings';

/** Same checks as the server, so the user knows before uploading. Returns an `errors.*` key or null. */
export function imageFileError(file: Pick<File, 'type' | 'size'>, maxMb: number): string | null {
  if (!ACCEPTED_IMAGE_TYPES.includes(file.type)) return 'errors.image.unsupportedType';
  if (file.size > maxMb * 1024 * 1024) return 'errors.image.tooLarge';
  return null;
}

/**
 * Pick an image → it is uploaded at once → `changed` emits it (or null when removed).
 * The parent saves the id with its own form. Removing only unlinks: the file stays until deleted.
 */
@Component({
  selector: 'app-image-upload',
  imports: [TranslocoPipe],
  template: `
    <div class="flex flex-wrap items-center gap-4 rounded-xl border-2 border-dashed border-line-strong bg-subtle/40 p-3 transition-colors duration-200 hover:border-brand sm:flex-nowrap">
      <div class="flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-card ring-1 ring-line">
        @if (url(); as src) {
          <img [src]="src" alt="" class="size-full object-cover" />
        } @else {
          <svg class="size-7 text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <rect x="3" y="4" width="18" height="16" rx="2" />
            <circle cx="9" cy="10" r="1.75" />
            <path d="m21 16-5-5-9 9" />
          </svg>
        }
      </div>
      <div class="min-w-0 flex-1 space-y-1.5 text-sm">
        <div class="flex flex-wrap items-center gap-3">
          <label class="btn btn-secondary btn-sm"
            [class.pointer-events-none]="progress() !== null" [class.opacity-50]="progress() !== null">
            {{ (url() ? 'images.replace' : 'images.choose') | transloco }}
            <input type="file" class="sr-only" [accept]="accept" (change)="pick($event)" />
          </label>
          @if (url() && progress() === null) {
            <button type="button" (click)="changed.emit(null)" class="btn btn-danger-soft btn-sm">{{ 'images.remove' | transloco }}</button>
          }
        </div>
        @if (progress() !== null) {
          <p class="text-muted" role="status">{{ 'images.uploading' | transloco: { percent: progress() } }}</p>
        } @else if (error(); as key) {
          <p class="text-danger" role="alert">{{ key | transloco: { max: maxMb() } }}</p>
        } @else {
          <p class="text-xs text-muted">{{ 'images.hint' | transloco: { max: maxMb() } }}</p>
        }
      </div>
    </div>
  `,
})
export class ImageUpload {
  private readonly api = inject(ImagesApi);
  private readonly settings = inject(PlatformSettings);

  readonly purpose = input.required<ImagePurpose>();
  /** Current image URL, if any. */
  readonly url = input<string | null>(null);
  readonly changed = output<UploadedImage | null>();

  protected readonly accept = ACCEPTED_IMAGE_TYPES.join(',');
  protected readonly progress = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly maxMb = () => this.settings.number('upload.maxImageSizeMb', 5);

  protected pick(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // picking the same file again must fire change again
    if (!file) return;

    const invalid = imageFileError(file, this.maxMb());
    this.error.set(invalid);
    if (invalid) return;

    this.progress.set(0);
    this.api.upload(file, this.purpose()).subscribe({
      next: (e) => {
        if (e.type === HttpEventType.UploadProgress && e.total) this.progress.set(Math.round((100 * e.loaded) / e.total));
        if (e.type === HttpEventType.Response && e.body) {
          this.progress.set(null);
          this.changed.emit(e.body);
        }
      },
      error: (err: unknown) => {
        this.progress.set(null);
        this.error.set(errorKey(err));
      },
    });
  }
}
