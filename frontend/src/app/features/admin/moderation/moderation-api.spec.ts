import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ModerationApi } from './moderation-api';

describe('ModerationApi', () => {
  it('calls the moderation endpoints with the reason', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(ModerationApi);
    const http = TestBed.inject(HttpTestingController);

    api.stores({ status: 'Disabled', deleted: undefined, page: 1 }).subscribe();
    api.products({ storeId: 's1', state: 'Disabled', sort: 'PriceDesc', minPrice: 100000 }).subscribe();
    api.disableStore('s1', 'Faux magasin').subscribe();
    api.enableProduct('p1').subscribe();
    api.disableProducts(['p1', 'p2'], 'Lot suspect').subscribe();

    expect(http.expectOne('/api/admin/stores?status=Disabled&page=1').request.method).toBe('GET');
    expect(http.expectOne('/api/admin/products?storeId=s1&state=Disabled&sort=PriceDesc&minPrice=100000').request.method).toBe('GET');
    expect(http.expectOne('/api/admin/stores/s1/disable').request.body).toEqual({ reason: 'Faux magasin' });
    expect(http.expectOne('/api/admin/products/p1/enable').request.body).toBeNull();
    expect(http.expectOne('/api/admin/products/disable').request.body).toEqual({ ids: ['p1', 'p2'], reason: 'Lot suspect' });
    http.verify();
  });
});
