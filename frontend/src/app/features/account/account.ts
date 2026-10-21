import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { RoleBadge } from '../../shared/ui/role-badge';
import { AffiliateCard } from './affiliate-card/affiliate-card';
import { RoleRequests } from './role-requests/role-requests';

@Component({
  selector: 'app-account',
  imports: [TranslocoPipe, DatePipe, RouterLink, AffiliateCard, RoleRequests, RoleBadge],
  templateUrl: './account.html',
})
export class Account {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
}
