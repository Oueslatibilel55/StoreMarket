import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { Modal } from '../../../shared/ui/modal';
import { AdminApi } from '../admin-api';
import { AdminSetting } from '../admin.models';
import { sameValue, settingError } from './setting-input';

/** Display order of the groups (each setting carries its group). */
const GROUPS = ['coins', 'affiliates', 'stores', 'search', 'general'];

@Component({
  selector: 'app-settings',
  imports: [DatePipe, TranslocoPipe, Modal],
  templateUrl: './settings.html',
})
export class Settings {
  private readonly api = inject(AdminApi);
  protected readonly language = inject(LanguageService);

  protected readonly settings = rxResource({ stream: () => this.api.settings() });
  protected readonly groups = computed(() => {
    const all = this.settings.value() ?? [];
    return GROUPS.map((group) => ({ group, items: all.filter((s) => s.group === group) })).filter((g) => g.items.length);
  });

  /** What the admin typed, per key, until saved. */
  protected readonly drafts = signal<Record<string, string>>({});
  protected readonly pending = signal<{ setting: AdminSetting; value: string } | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly savedKey = signal<string | null>(null);

  protected draft(setting: AdminSetting): string {
    return this.drafts()[setting.key] ?? setting.value;
  }

  protected edit(setting: AdminSetting, value: string): void {
    this.drafts.update((d) => ({ ...d, [setting.key]: value }));
    this.savedKey.set(null);
  }

  protected invalid(setting: AdminSetting): string | null {
    return setting.editable ? settingError(setting, this.draft(setting)) : null;
  }

  protected changed(setting: AdminSetting): boolean {
    return !sameValue(setting, this.draft(setting));
  }

  protected reset(setting: AdminSetting): void {
    this.drafts.update(({ [setting.key]: _, ...rest }) => rest);
  }

  protected ask(setting: AdminSetting): void {
    if (this.invalid(setting) || !this.changed(setting)) return;
    this.error.set(null);
    this.reason.set('');
    this.pending.set({ setting, value: this.draft(setting).trim() });
  }

  protected cancel(): void {
    if (!this.busy()) this.pending.set(null);
  }

  protected confirm(): void {
    const change = this.pending();
    if (!change) return;
    this.busy.set(true);

    this.api.updateSetting(change.setting.key, change.value, this.reason().trim() || null).subscribe({
      next: (updated) => {
        this.settings.update((list) => list?.map((s) => (s.key === updated.key ? updated : s)));
        this.reset(updated);
        this.savedKey.set(updated.key);
        this.busy.set(false);
        this.pending.set(null);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
