import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface PdfMergeResponse {
  pdfBase64: string;
  pageCount: number;
  fileName: string;
}

@Injectable({
  providedIn: 'root'
})
export class PdfService {
  private apiUrl = '/api/pdf';

  constructor(private http: HttpClient) {}

  mergePdfs(files: File[]): Observable<PdfMergeResponse> {
    const formData = new FormData();
    files.forEach(file => {
      formData.append('files', file, file.name);
    });
    return this.http.post<PdfMergeResponse>(`${this.apiUrl}/merge`, formData);
  }
}
