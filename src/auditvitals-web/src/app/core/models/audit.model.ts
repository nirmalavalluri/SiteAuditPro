export interface SubmitAuditRequest {
  url: string;
  pageLimit?: number | null;
}

export interface SubmitAuditResponse {
  auditId: string;
  statusUrl: string;
}

export type AuditStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export interface AuditStatusResponse {
  auditId: string;
  status: AuditStatus;
  requestedUrl: string;
  requestedPageLimit: number;
  crawledPageCount: number;
  discoveredPageCount: number;
  healthScore: number | null;
  isSampledAudit: boolean;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  failedAtUtc: string | null;
  failureReason: string | null;
}

/** Shape of an ASP.NET Core ValidationProblemDetails response. */
export interface ValidationProblemDetails {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
