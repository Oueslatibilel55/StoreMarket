import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthShell } from '../../../shared/layout/auth-shell';
import { AccountType } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { errorKey } from '../../../core/http/problem';

/** Mirrors the backend rule (User.Register): 8+ characters with at least one letter and one digit. */
const PASSWORD_PATTERN = /^(?=.*\p{L})(?=.*\d).{8,128}$/u;
/** Tunisian number: 8 digits, optional +216 / 00216, spaces/dots/dashes allowed. */
const PHONE_PATTERN = /^(\+216|00216)?[\s.-]*(\d[\s.-]*){8}$/;

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe, AuthShell],
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
    password: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
    accountType: ['Buyer' as AccountType],
    acceptAffiliateTerms: [false],
  });
  protected readonly accountTypes: AccountType[] = ['Buyer', 'Affiliate'];
  protected readonly accountType = toSignal(this.form.controls.accountType.valueChanges, { initialValue: 'Buyer' as AccountType });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected submit(): void {
    if (this.form.invalid || this.termsMissing()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);

    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => this.router.navigateByUrl('/account'),
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.submitting.set(false);
      },
    });
  }

  /** An affiliate account needs the terms accepted; a client account doesn't. */
  protected termsMissing(): boolean {
    const { accountType, acceptAffiliateTerms } = this.form.getRawValue();
    return accountType === 'Affiliate' && !acceptAffiliateTerms;
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.touched && control.invalid;
  }
}
