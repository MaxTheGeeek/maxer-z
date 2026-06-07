import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AppSettings, McpConfig } from '../models/models';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private get base(): string {
    if (window.location.hostname === 'localhost' && window.location.port !== '4200') {
      return `${window.location.origin}/api/settings`;
    }
    return 'http://localhost:5000/api/settings';
  }

  constructor(private http: HttpClient) {}

  getSettings() { 
    return this.http.get<AppSettings>(this.base); 
  }
  
  saveSettings(s: AppSettings) { 
    return this.http.post(this.base, s); 
  }
  
  getMcpConfig() { 
    return this.http.get<McpConfig>(`${this.base}/mcp`); 
  }
  
  saveMcpConfig(c: McpConfig) { 
    return this.http.post(`${this.base}/mcp`, c); 
  }

  getActiveProviders() {
    return this.http.get<{ providers: any[]; priority: string[] }>(
      `${this.base}/active-providers`);
  }

  testProvider(id: string, settings: AppSettings) {
    return this.http.post<{ success: boolean; model?: string; error?: string }>(
      `${this.base}/test-provider/${id}`, settings);
  }

  testMcp() {
    return this.http.post<{ success: boolean }>(
      `${this.base}/test-mcp`, {});
  }
}
