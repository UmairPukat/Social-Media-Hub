import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { PlatformCard } from '../../core/models/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  readonly connected = signal(0);
  readonly total = signal(0);

  constructor(public auth: AuthService, private api: ApiService) {}

  ngOnInit(): void {
    this.api.getPlatforms().subscribe({
      next: (res) => {
        const cards: PlatformCard[] = res.data || [];
        this.total.set(cards.filter((c) => c.isImplemented).length);
        this.connected.set(cards.filter((c) => c.isConnected).length);
      }
    });
  }
}
