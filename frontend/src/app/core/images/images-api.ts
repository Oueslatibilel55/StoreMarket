import { HttpClient, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type ImagePurpose = 'StoreLogo' | 'StoreBanner' | 'ProductPhoto' | 'CategoryImage';

export interface UploadedImage {
  id: string;
  /** Served by the API as a static file: /uploads/... */
  url: string;
  purpose: ImagePurpose;
  contentType: string;
  sizeBytes: number;
  originalName: string | null;
}

/** Same list as the backend (ImageSignature): no SVG, no GIF. */
export const ACCEPTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

@Injectable({ providedIn: 'root' })
export class ImagesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/images`;

  /** Emits progress events, then the response with the stored image. */
  upload(file: File, purpose: ImagePurpose): Observable<HttpEvent<UploadedImage>> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('purpose', purpose);
    return this.http.post<UploadedImage>(this.base, form, { reportProgress: true, observe: 'events' });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
