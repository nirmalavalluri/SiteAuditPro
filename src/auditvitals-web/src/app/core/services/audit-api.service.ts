import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuditStatusResponse,
  SubmitAuditRequest,
  SubmitAuditResponse,
} from '../models/audit.model';

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly baseUrl = '/api/audits';

  constructor(private readonly http: HttpClient) {}

  submitAudit(request: SubmitAuditRequest): Observable<SubmitAuditResponse> {
    return this.http.post<SubmitAuditResponse>(this.baseUrl, request);
  }

  getAuditStatus(auditId: string): Observable<AuditStatusResponse> {
    return this.http.get<AuditStatusResponse>(`${this.baseUrl}/${auditId}`);
  }
}
