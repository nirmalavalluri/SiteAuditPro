import { Component, input } from '@angular/core';

export type IconName = 'search' | 'globe' | 'sun' | 'moon' | 'error';

@Component({
  selector: 'app-icon',
  standalone: true,
  templateUrl: './icon.html',
  styleUrl: './icon.scss',
})
export class Icon {
  readonly name = input.required<IconName>();
}
