import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { TranslocoPipe } from '@jsverse/transloco';

interface NavItem {
  key: string;
  link?: string;
  /** Feature number that will bring this section to life. */
  soon?: string;
}

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe],
  templateUrl: './admin-layout.html',
})
export class AdminLayout {
  protected readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), map(() => this.router.url)),
    { initialValue: this.router.url },
  );
  /** Section of the current page, for the phone menu (a detail page belongs to its list). */
  protected readonly current = computed(() => {
    const path = this.url().split('?')[0];
    return this.nav.find((i) => i.link && (path === i.link || path.startsWith(i.link + '/')))?.link ?? '';
  });
  protected readonly links = computed(() => this.nav.filter((i) => i.link));

  protected readonly nav: NavItem[] = [
    { key: 'users', link: '/admin/users' },
    { key: 'roleRequests', link: '/admin/role-requests' },
    { key: 'audit', link: '/admin/audit' },
    { key: 'settings', link: '/admin/settings' },
    { key: 'storeCategories', link: '/admin/store-categories' },
    { key: 'productCategories', link: '/admin/product-categories' },
    { key: 'stores', link: '/admin/stores' },
    { key: 'products', link: '/admin/products' },
    { key: 'partnerships', link: '/admin/partnerships' },
    { key: 'orders', link: '/admin/orders' },
    { key: 'reviews', soon: '017' },
    { key: 'coins', link: '/admin/coins' },
    { key: 'payouts', link: '/admin/payouts' },
    { key: 'billing', soon: '025' },
  ];
}
