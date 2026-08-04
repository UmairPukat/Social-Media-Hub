import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss'
})
export class AnalyticsComponent {
  readonly metrics = [
    { label: 'Published posts', value: '—', hint: 'Connect platforms to unlock metrics' },
    { label: 'Inbox volume', value: '—', hint: 'Comments + messages this week' },
    { label: 'Response rate', value: '—', hint: 'Coming with deeper Meta insights' }
  ];
}
