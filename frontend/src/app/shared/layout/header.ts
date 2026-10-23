import { NgTemplateOutlet } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { CartService } from '../../core/cart/cart.service';
import { LanguageSwitcher } from '../language-switcher/language-switcher';

@Component({
  selector: 'app-header',
  imports: [NgTemplateOutlet, RouterLink, RouterLinkActive, TranslocoPipe, LanguageSwitcher],
  templateUrl: './header.html',
})
export class Header {
  protected readonly auth = inject(AuthService);
  protected readonly cart = inject(CartService);
  private readonly router = inject(Router);

  /** The catalogue is the search page: stores then products, best rated first (018). */
  protected search(event: Event, text: string): void {
    event.preventDefault();
    this.router.navigate(['/products'], { queryParams: { search: text.trim() || null } });
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => this.router.navigateByUrl('/'));
  }
}
