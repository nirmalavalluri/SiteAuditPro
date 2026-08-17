import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { ThemeService } from './core/services/theme.service';
import { Icon } from './shared/components/icon/icon';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, MatToolbarModule, MatButtonModule, Icon],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly theme = inject(ThemeService);
  protected readonly currentYear = new Date().getFullYear();

  toggleTheme(): void {
    this.theme.toggle();
  }
}
