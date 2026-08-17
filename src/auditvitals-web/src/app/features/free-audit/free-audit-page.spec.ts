import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FreeAuditPage } from './free-audit-page';
import { AuditStatusResponse, SubmitAuditResponse } from '../../core/models/audit.model';

describe('FreeAuditPage', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FreeAuditPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
  });

  function createComponent() {
    const fixture = TestBed.createComponent(FreeAuditPage);
    fixture.detectChanges();
    return fixture;
  }

  it('does not submit when the URL field is empty', () => {
    const fixture = createComponent();
    const component = fixture.componentInstance;

    component.runFreeAudit();

    httpMock.expectNone('/api/audits');
    expect(component['urlControl'].touched).toBe(true);
  });

  it('submits a valid URL and polls until the audit completes', async () => {
    vi.useFakeTimers();

    const fixture = createComponent();
    const component = fixture.componentInstance;
    component['urlControl'].setValue('https://example.com');

    component.runFreeAudit();

    const submitReq = httpMock.expectOne('/api/audits');
    expect(submitReq.request.method).toBe('POST');
    submitReq.flush({
      auditId: 'audit-1',
      statusUrl: '/api/audits/audit-1',
    } satisfies SubmitAuditResponse);

    await vi.advanceTimersByTimeAsync(0);

    const firstPoll = httpMock.expectOne('/api/audits/audit-1');
    firstPoll.flush({
      auditId: 'audit-1',
      status: 'Queued',
      requestedUrl: 'https://example.com/',
      requestedPageLimit: 25,
      crawledPageCount: 0,
      discoveredPageCount: 0,
      healthScore: null,
      isSampledAudit: false,
      createdAtUtc: '2026-01-01T00:00:00Z',
      startedAtUtc: null,
      completedAtUtc: null,
      failedAtUtc: null,
      failureReason: null,
    } satisfies AuditStatusResponse);

    expect(component['status']()?.status).toBe('Queued');

    await vi.advanceTimersByTimeAsync(2000);

    const secondPoll = httpMock.expectOne('/api/audits/audit-1');
    secondPoll.flush({
      auditId: 'audit-1',
      status: 'Completed',
      requestedUrl: 'https://example.com/',
      requestedPageLimit: 25,
      crawledPageCount: 1,
      discoveredPageCount: 1,
      healthScore: null,
      isSampledAudit: true,
      createdAtUtc: '2026-01-01T00:00:00Z',
      startedAtUtc: '2026-01-01T00:00:01Z',
      completedAtUtc: '2026-01-01T00:00:02Z',
      failedAtUtc: null,
      failureReason: null,
    } satisfies AuditStatusResponse);

    expect(component['status']()?.status).toBe('Completed');
    expect(component['submitting']()).toBe(false);

    // No further polling once terminal.
    await vi.advanceTimersByTimeAsync(4000);
    httpMock.expectNone('/api/audits/audit-1');
  });

  it('surfaces a server validation error without polling', async () => {
    vi.useFakeTimers();

    const fixture = createComponent();
    const component = fixture.componentInstance;
    component['urlControl'].setValue('http://127.0.0.1/');

    component.runFreeAudit();

    const submitReq = httpMock.expectOne('/api/audits');
    submitReq.flush(
      { title: 'Validation failed', errors: { url: ["Host '127.0.0.1' is not allowed."] } },
      { status: 400, statusText: 'Bad Request' },
    );

    await vi.advanceTimersByTimeAsync(0);

    expect(component['submitError']()).toContain('127.0.0.1');
    expect(component['submitting']()).toBe(false);
    httpMock.expectNone('/api/audits/audit-1');
  });
});
