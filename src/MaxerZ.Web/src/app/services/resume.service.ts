import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ResumeRequest, ResumeRecord, ResumeResult } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ResumeService {
  private get base(): string {
    if (window.location.hostname === 'localhost' && window.location.port !== '4200') {
      return `${window.location.origin}/api/resume`;
    }
    return 'http://localhost:5000/api/resume';
  }

  getPreviewPdfUrl(): string {
    return `${this.base}/preview-pdf`;
  }

  private previewState: { result: ResumeResult; request: ResumeRequest; lang: string } | null = null;
  private resumeComposeState: any = null;

  constructor(private http: HttpClient) {}

  preview(req: ResumeRequest) { 
    return this.http.post<ResumeResult>(`${this.base}/preview`, req); 
  }
  
  export(req: ResumeRequest) { 
    return this.http.post<ResumeResult>(`${this.base}/export`, req); 
  }
  
  history() { 
    return this.http.get<ResumeRecord[]>(`${this.base}/history`); 
  }

  setPreviewData(result: ResumeResult, request: ResumeRequest, lang: string) {
    this.previewState = { result, request, lang };
  }
  
  getPreviewData() { 
    return this.previewState; 
  }

  setResumeComposeState(state: any) { 
    this.resumeComposeState = state; 
  }
  
  getResumeComposeState() { 
    return this.resumeComposeState; 
  }
}
