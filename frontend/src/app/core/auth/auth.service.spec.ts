import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthResponse } from './auth.models';
import { AuthService } from './auth.service';

const response: AuthResponse = {
  accessToken: 'token-1',
  accessTokenExpiresAt: '2026-10-19T12:15:00Z',
  user: {
    id: '1',
    email: 'amira@example.com',
    fullName: 'Amira',
    phone: '+21622123456',
    roles: ['Buyer'],
    affiliateCode: null,
    createdAt: '2026-10-19T12:00:00Z',
  },
};

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('stores the session after login', () => {
    auth.login({ email: 'amira@example.com', password: 'Secret123' }).subscribe();
    http.expectOne('/api/auth/login').flush(response);

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('token-1');
    expect(auth.hasRole('Buyer')).toBe(true);
    expect(auth.hasRole('Admin')).toBe(false);
  });

  it('signs up with an affiliate account in one call: the first token already has the role', () => {
    const request = {
      email: 'sana@example.com', password: 'Secret123', fullName: 'Sana', phone: '55 321 987',
      accountType: 'Affiliate' as const, acceptAffiliateTerms: true,
    };
    auth.register(request).subscribe();

    const call = http.expectOne('/api/auth/register');
    expect(call.request.body).toEqual(request);
    call.flush({ ...response, user: { ...response.user, roles: ['Affiliate'], affiliateCode: 'DRF-45' } });

    expect(auth.hasRole('Affiliate')).toBe(true);
    expect(auth.hasRole('Buyer')).toBe(false);
    expect(auth.user()?.affiliateCode).toBe('DRF-45');
  });

  it('shares one refresh request between concurrent callers', () => {
    const results: (string | null)[] = [];
    auth.refresh().subscribe((t) => results.push(t));
    auth.refresh().subscribe((t) => results.push(t));

    http.expectOne('/api/auth/refresh').flush({ ...response, accessToken: 'token-2' });

    expect(results).toEqual(['token-2', 'token-2']);
  });

  it('clears the session when refresh fails', () => {
    auth.login({ email: 'amira@example.com', password: 'Secret123' }).subscribe();
    http.expectOne('/api/auth/login').flush(response);

    let result: string | null | undefined;
    auth.refresh().subscribe((t) => (result = t));
    http.expectOne('/api/auth/refresh').flush({ code: 'auth.invalidRefreshToken' }, { status: 401, statusText: 'Unauthorized' });

    expect(result).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('clears the session on logout even if the call fails', () => {
    auth.login({ email: 'amira@example.com', password: 'Secret123' }).subscribe();
    http.expectOne('/api/auth/login').flush(response);

    auth.logout().subscribe();
    http.expectOne('/api/auth/logout').flush(null, { status: 500, statusText: 'Error' });

    expect(auth.isAuthenticated()).toBe(false);
  });
});
