import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { catchError, of, switchMap, takeWhile, timer } from 'rxjs';
import { AuditApiService } from '../../core/services/audit-api.service';
import { AuditStatusResponse, ValidationProblemDetails } from '../../core/models/audit.model';
import { Icon } from '../../shared/components/icon/icon';
import { StatusBadge } from '../../shared/components/status-badge/status-badge';

const POLL_INTERVAL_MS = 2000;
const IN_PROGRESS_STATUSES = new Set(['Queued', 'Running']);

@Component({
  selector: 'app-free-audit-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    Icon,
    StatusBadge,
  ],
  templateUrl: './free-audit-page.html',
  styleUrl: './free-audit-page.scss',
})
export class FreeAuditPage {
  private readonly api = inject(AuditApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly form = this.formBuilder.group({
    url: [
      '',
      [Validators.required, Validators.pattern(/^https?:\/\/.+/i), Validators.maxLength(2048)],
    ],
  });

  protected readonly submitting = signal(false);
  protected readonly submitError = signal<string | null>(null);
  protected readonly status = signal<AuditStatusResponse | null>(null);

  protected get urlControl() {
    return this.form.controls.url;
  }

  protected get inProgress(): boolean {
    const current = this.status();
    return current !== null && IN_PROGRESS_STATUSES.has(current.status);
  }

  runFreeAudit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const url = this.urlControl.value ?? '';
    this.submitting.set(true);
    this.submitError.set(null);
    this.status.set(null);

    this.api
      .submitAudit({ url })
      .pipe(
        catchError((error: HttpErrorResponse) => {
          this.submitError.set(this.describeError(error));
          this.submitting.set(false);
          return of(null);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((response) => {
        if (!response) {
          return;
        }
        this.pollStatus(response.auditId);
      });
  }

  private pollStatus(auditId: string): void {
    timer(0, POLL_INTERVAL_MS)
      .pipe(
        switchMap(() => this.api.getAuditStatus(auditId)),
        catchError((error: HttpErrorResponse) => {
          this.submitError.set(this.describeError(error));
          this.submitting.set(false);
          return of(null);
        }),
        takeWhile((result) => result === null || IN_PROGRESS_STATUSES.has(result.status), true),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.status.set(result);
        this.submitting.set(false);
      });
  }

  private describeError(error: HttpErrorResponse): string {
    const problem = error.error as ValidationProblemDetails | null;
    if (problem?.errors) {
      const messages = Object.values(problem.errors).flat();
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }

    if (error.status === 429) {
      return 'Too many audit requests from this network. Please wait a minute and try again.';
    }

    return 'Something went wrong submitting your audit. Please try again.';
  }
}
