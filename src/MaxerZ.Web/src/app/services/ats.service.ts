import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AtsRequest, AtsResult } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class AtsService {
  private apiUrl = '/api/ats';

  constructor(private http: HttpClient) {}

  analyze(request: AtsRequest): Observable<AtsResult> {
    return this.http.post<AtsResult>(`${this.apiUrl}/analyze`, request);
  }
}
