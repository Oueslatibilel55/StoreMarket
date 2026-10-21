import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { roleGuard } from './auth.guards';
import { AuthService } from './auth.service';

describe('roleGuard', () => {
  const run = (signedIn: boolean, hasRole: boolean) => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { isAuthenticated: () => signedIn, hasRole: () => hasRole } }],
    });
    return TestBed.runInInjectionContext(() =>
      roleGuard('Admin')({} as ActivatedRouteSnapshot, { url: '/admin' } as RouterStateSnapshot),
    );
  };
  const url = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('lets users with the role through', () => expect(run(true, true)).toBe(true));
  it('sends signed-in users without the role to their account', () => expect(url(run(true, false))).toBe('/account'));
  it('sends signed-out users to login with a return URL', () => expect(url(run(false, false))).toBe('/login?returnUrl=%2Fadmin'));
});
