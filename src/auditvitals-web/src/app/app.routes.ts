import { Routes } from '@angular/router';
import { FreeAuditPage } from './features/free-audit/free-audit-page';

export const routes: Routes = [
  { path: '', component: FreeAuditPage, title: 'AuditVitals — Free Website Audit' },
  { path: '**', redirectTo: '' },
];
