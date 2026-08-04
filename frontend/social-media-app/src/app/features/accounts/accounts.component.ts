import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { ApiService } from '../../core/services/api.service';
import { PlatformCard } from '../../core/models/models';

@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule],
  templateUrl: './accounts.component.html',
  styleUrl: './accounts.component.scss'
})
export class AccountsComponent implements OnInit {
  readonly accounts = signal<PlatformCard[]>([]);

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.getPlatforms().subscribe({
      next: (res) => this.accounts.set((res.data || []).filter((p) => p.isConnected))
    });
  }
}
