import { imageFileError } from './image-upload';

describe('imageFileError', () => {
  const mb = 1024 * 1024;

  it('accepts JPEG / PNG / WebP under the limit', () => {
    expect(imageFileError({ type: 'image/webp', size: 2 * mb }, 5)).toBeNull();
  });

  it('refuses other types, SVG included', () => {
    expect(imageFileError({ type: 'image/svg+xml', size: 100 }, 5)).toBe('errors.image.unsupportedType');
    expect(imageFileError({ type: 'application/pdf', size: 100 }, 5)).toBe('errors.image.unsupportedType');
  });

  it('refuses files above the setting', () => {
    expect(imageFileError({ type: 'image/png', size: 5 * mb + 1 }, 5)).toBe('errors.image.tooLarge');
  });
});
