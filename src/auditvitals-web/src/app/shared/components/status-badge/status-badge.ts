import { Component, computed, input } from '@angular/core';
import { AuditStatus } from '../../../core/models/audit.model';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  templateUrl: './status-badge.html',
  styleUrl: './status-badge.scss',
})
export class StatusBadge {
  readonly status = input.required<AuditStatus>();

  protected readonly label = computed(() => this.status());
  protected readonly cssClass = computed(
    () => `status-badge status-badge--${this.status().toLowerCase()}`,
  );
}
