import { formatDate } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService } from './language.service';
import { LANGUAGES } from './languages';

describe('LanguageService', () => {
  let service: LanguageService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [TranslocoTestingModule.forRoot({ langs: { en: {}, fr: {}, ar: {} } })],
    });
    service = TestBed.inject(LanguageService);
  });

  it('switches <html> to RTL for Arabic', () => {
    service.use('ar');

    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(service.dir()).toBe('rtl');
  });

  it('switches back to LTR for French', () => {
    service.use('ar');
    service.use('fr');

    expect(document.documentElement.dir).toBe('ltr');
  });

  it('restores the saved language', () => {
    localStorage.setItem('lang', 'en');

    service.init();

    expect(service.lang()).toBe('en');
  });
});

describe('LANGUAGES', () => {
  it.each(LANGUAGES.map((l) => [l.code, l.locale]))('%s has locale data for date pipes (%s)', (_code, locale) => {
    expect(() => formatDate('2026-10-19', 'longDate', locale)).not.toThrow();
  });
});
