import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReviewApi } from './review-api';

describe('ReviewApi', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));

  it('reads public reviews with only the filters that are set', () => {
    const api = TestBed.inject(ReviewApi);
    const http = TestBed.inject(HttpTestingController);

    api.productReviews('musc-blanc', { stars: 5, page: 2 }).subscribe();
    api.storeReviews('boutique-amel').subscribe();

    expect(http.expectOne('/api/products/musc-blanc/reviews?stars=5&page=2').request.method).toBe('GET');
    expect(http.expectOne('/api/stores/boutique-amel/reviews').request.method).toBe('GET');
    http.verify();
  });

  it('rates a product line or the store, edits, replies and moderates', () => {
    const api = TestBed.inject(ReviewApi);
    const http = TestBed.inject(HttpTestingController);

    api.create({ storeOrderId: 'so1', orderLineId: null, stars: 4, comment: null }).subscribe();
    api.edit('r1', { stars: 2, comment: 'Déçue' }).subscribe();
    api.reply('r1', 'Merci !').subscribe();
    api.removeReply('r1').subscribe();
    api.hide('r1', 'Insultes').subscribe();
    api.adminList({ hidden: true, page: 1 }).subscribe();

    const create = http.expectOne('/api/me/reviews');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ storeOrderId: 'so1', orderLineId: null, stars: 4, comment: null });
    expect(http.expectOne('/api/me/reviews/r1').request.method).toBe('PUT');
    const reply = http.expectOne((r) => r.url === '/api/me/store/reviews/r1/reply' && r.method === 'PUT');
    expect(reply.request.body).toEqual({ reply: 'Merci !' });
    expect(http.expectOne((r) => r.url === '/api/me/store/reviews/r1/reply' && r.method === 'DELETE')).toBeTruthy();
    expect(http.expectOne('/api/admin/reviews/r1/hide').request.body).toEqual({ reason: 'Insultes' });
    expect(http.expectOne('/api/admin/reviews?hidden=true&page=1').request.method).toBe('GET');
    http.verify();
  });
});
