import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { ScreeningSummary } from '../../models/screening.model';

@Component({
  selector: 'app-screenings',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './screenings.component.html',
  styleUrl: './screenings.component.css'
})
export class ScreeningsComponent implements OnInit {
  private readonly api = inject(ApiService);

  screenings: ScreeningSummary[] = [];
  isLoading = true;
  error = '';
  deletingId: number | null = null;
  confirmDeleteId: number | null = null;

  ngOnInit() {
    this.load();
  }

  load() {
    this.isLoading = true;
    this.api.getScreeningsSummary().subscribe({
      next: data => {
        this.screenings = data;
        this.isLoading = false;
      },
      error: () => {
        this.error = 'Failed to load screenings. Is the API running?';
        this.isLoading = false;
      }
    });
  }

  requestDelete(id: number, event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.confirmDeleteId = id;
  }

  cancelDelete() {
    this.confirmDeleteId = null;
  }

  confirmDelete(id: number) {
    this.deletingId = id;
    this.confirmDeleteId = null;
    this.api.deleteJob(id).subscribe({
      next: () => {
        this.screenings = this.screenings.filter(s => s.id !== id);
        this.deletingId = null;
      },
      error: () => {
        this.deletingId = null;
      }
    });
  }

  getScoreColor(score: number): string {
    if (score >= 80) return '#22c55e';
    if (score >= 60) return '#f59e0b';
    return '#ef4444';
  }

  getScoreLabel(score: number): string {
    if (score >= 80) return 'Strong';
    if (score >= 60) return 'Mixed';
    if (score > 0) return 'Weak';
    return '—';
  }

  formatDate(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  get pendingCount(): number {
    return this.screenings.reduce((acc, s) =>
      acc + (s.totalCandidates - s.approvedCount - s.rejectedCount), 0);
  }

  trackById(_: number, s: ScreeningSummary) { return s.id; }
}
