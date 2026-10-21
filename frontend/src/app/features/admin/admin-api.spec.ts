import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminApi, toParams } from './admin-api';

describe('AdminApi', () => {
  it('only sends the filters that are set', () => {
    expect(toParams({ search: 'amira', role: undefined, status: '', page: 2 }).toString()).toBe('search=amira&page=2');
  });

  it('calls the admin endpoints', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(AdminApi);
    const http = TestBed.inject(HttpTestingController);

    api.listUsers({ role: 'Affiliate', page: 1 }).subscribe();
    api.block('42', 'Fake orders').subscribe();
    api.revokeRole('42', 'Admin').subscribe();

    expect(http.expectOne('/api/admin/users?role=Affiliate&page=1').request.method).toBe('GET');
    const block = http.expectOne('/api/admin/users/42/block');
    expect(block.request.body).toEqual({ reason: 'Fake orders' });
    expect(http.expectOne('/api/admin/users/42/roles/Admin').request.method).toBe('DELETE');
    http.verify();
  });
});
