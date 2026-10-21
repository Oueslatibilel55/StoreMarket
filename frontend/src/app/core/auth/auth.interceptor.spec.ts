import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  it('adds the bearer token to API calls', () => {
    vi.spyOn(auth, 'accessToken').mockReturnValue('abc');

    client.get('/api/ping').subscribe();

    expect(http.expectOne('/api/ping').request.headers.get('Authorization')).toBe('Bearer abc');
  });

  it('does not add the token to non-API calls', () => {
    vi.spyOn(auth, 'accessToken').mockReturnValue('abc');

    client.get('/i18n/fr.json').subscribe();

    expect(http.expectOne('/i18n/fr.json').request.headers.has('Authorization')).toBe(false);
  });

  it('refreshes once on 401 and replays the request with the new token', () => {
    vi.spyOn(auth, 'accessToken').mockReturnValue('old');
    let body: unknown;

    client.get('/api/orders').subscribe((b) => (body = b));
    http.expectOne('/api/orders').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/auth/refresh').flush({
      accessToken: 'new',
      accessTokenExpiresAt: '',
      user: { id: '1', email: 'a@b.tn', fullName: 'A', phone: '', roles: ['Buyer'], affiliateCode: null, createdAt: '' },
    });
    const retry = http.expectOne('/api/orders');
    retry.flush({ ok: true });

    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    expect(body).toEqual({ ok: true });
  });
});
