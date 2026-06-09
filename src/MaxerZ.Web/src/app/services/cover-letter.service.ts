import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CoverLetterRequest, CoverLetterRecord, LlmResult } from '../models/models';

@Injectable({ providedIn: 'root' })
export class CoverLetterService {
  private get base(): string {
    if (window.location.hostname === 'localhost' && window.location.port !== '4200') {
      return `${window.location.origin}/api/coverletter`;
    }
    return 'http://localhost:5000/api/coverletter';
  }

  getPreviewPdfUrl(): string {
    return `${this.base}/preview-pdf`;
  }

  private previewState: { result: LlmResult; request: CoverLetterRequest; lang: string } | null = null;
  private composeState: any = null;

  constructor(private http: HttpClient) {}

  preview(req: CoverLetterRequest) { 
    return this.http.post<LlmResult>(`${this.base}/preview`, req); 
  }
  
  export(req: CoverLetterRequest) { 
    return this.http.post<LlmResult>(`${this.base}/export`, req); 
  }
  
  history() { 
    return this.http.get<CoverLetterRecord[]>(`${this.base}/history`); 
  }

  setPreviewData(result: LlmResult, request: CoverLetterRequest, lang: string) {
    this.previewState = { result, request, lang };
  }
  
  getPreviewData() { 
    return this.previewState; 
  }

  setComposeState(state: any) { 
    this.composeState = state; 
  }
  
  getComposeState() { 
    return this.composeState; 
  }
}
