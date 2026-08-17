import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'auditvitals-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly mode = signal<ThemeMode>(this.resolveInitialMode());

  constructor() {
    this.applyToDocument(this.mode());
  }

  toggle(): void {
    this.setMode(this.mode() === 'dark' ? 'light' : 'dark');
  }

  setMode(mode: ThemeMode): void {
    this.mode.set(mode);
    this.applyToDocument(mode);
    localStorage.setItem(STORAGE_KEY, mode);
  }

  private resolveInitialMode(): ThemeMode {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') {
      return stored;
    }

    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  private applyToDocument(mode: ThemeMode): void {
    document.documentElement.setAttribute('data-theme', mode);
  }
}
