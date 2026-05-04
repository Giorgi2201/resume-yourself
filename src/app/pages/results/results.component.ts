import { Component, ElementRef, HostListener, ViewChild, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { ApiService } from '../../services/api.service';
import { CandidateResult, ResultsResponse, UploadFileResult } from '../../models/result.model';

@Component({
  selector: 'app-results',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './results.component.html',
  styleUrl: './results.component.css'
})
export class ResultsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);

  results: ResultsResponse | null = null;
  isLoading = true;
  error = '';
  selectedCandidate: CandidateResult | null = null;
  feedbackLoading: Record<number, boolean> = {};
  isUploadModalOpen = false;
  isDropActive = false;
  isUploading = false;
  uploadGlobalProgress = 0;
  uploadError = '';
  uploadQueue: UploadQueueItem[] = [];
  isDescriptionModalOpen = false;
  private lastFocusedBeforeDescriptionModal: HTMLElement | null = null;
  @ViewChild('descriptionModalPanel') descriptionModalPanel?: ElementRef<HTMLDivElement>;

  get jobId(): number {
    return Number(this.route.snapshot.paramMap.get('jobId'));
  }

  ngOnInit() {
    this.loadResults();
  }

  loadResults() {
    this.isLoading = true;
    this.api.getResults(this.jobId, 1, 50).subscribe({
      next: data => {
        this.results = data;
        this.isLoading = false;
      },
      error: () => {
        this.error = 'Failed to load results.';
        this.isLoading = false;
      }
    });
  }

  openUploadModal() {
    this.isUploadModalOpen = true;
    this.uploadError = '';
  }

  closeUploadModal() {
    if (this.isUploading) return;
    this.isUploadModalOpen = false;
    this.uploadError = '';
    this.uploadGlobalProgress = 0;
    this.uploadQueue = [];
    this.isDropActive = false;
  }

  onUploadDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDropActive = true;
  }

  onUploadDragLeave() {
    this.isDropActive = false;
  }

  onUploadDrop(event: DragEvent) {
    event.preventDefault();
    this.isDropActive = false;
    const files = Array.from(event.dataTransfer?.files ?? []);
    this.addUploadFiles(files);
  }

  onUploadInputChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    this.addUploadFiles(files);
    input.value = '';
  }

  private addUploadFiles(files: File[]) {
    const existingNames = new Set(this.uploadQueue.map(i => i.file.name.toLowerCase()));
    for (const file of files) {
      const ext = file.name.toLowerCase().split('.').pop();
      if (ext !== 'pdf' && ext !== 'docx') {
        this.uploadQueue.push({
          file,
          status: 'failed',
          progress: 0,
          message: 'Invalid type. Only PDF and DOCX are allowed.'
        });
        continue;
      }

      if (existingNames.has(file.name.toLowerCase())) {
        continue;
      }

      this.uploadQueue.push({
        file,
        status: 'queued',
        progress: 0,
        message: ''
      });
      existingNames.add(file.name.toLowerCase());
    }
  }

  removeUploadItem(index: number) {
    if (this.isUploading) return;
    this.uploadQueue = this.uploadQueue.filter((_, i) => i !== index);
  }

  startUpload() {
    const pending = this.uploadQueue.filter(i => i.status === 'queued').map(i => i.file);
    if (pending.length === 0 || this.isUploading) return;

    this.isUploading = true;
    this.uploadError = '';
    this.uploadGlobalProgress = 0;

    this.uploadQueue = this.uploadQueue.map(i =>
      i.status === 'queued'
        ? { ...i, status: 'uploading', progress: 0, message: 'Uploading…' }
        : i
    );

    this.api.uploadCandidatesForExistingJob(this.jobId, pending).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress) {
          const total = event.total ?? 1;
          const pct = Math.max(0, Math.min(100, Math.round((event.loaded / total) * 100)));
          this.uploadGlobalProgress = pct;

          this.uploadQueue = this.uploadQueue.map(i =>
            i.status === 'uploading'
              ? {
                  ...i,
                  progress: pct,
                  status: pct >= 100 ? 'processing' : 'uploading',
                  message: pct >= 100 ? 'Processing…' : 'Uploading…'
                }
              : i
          );
        }

        if (event.type === HttpEventType.Response && event.body) {
          const fileMap = new Map(event.body.files.map(f => [f.fileName.toLowerCase(), f]));
          this.applyUploadResults(fileMap);
          this.refreshResultsAfterUpload();
        }
      },
      error: err => {
        this.isUploading = false;
        this.uploadError = err?.error?.message ?? 'Upload failed. Please try again.';
        this.uploadQueue = this.uploadQueue.map(i =>
          i.status === 'uploading' || i.status === 'processing'
            ? { ...i, status: 'failed', message: this.uploadError }
            : i
        );
      }
    });
  }

  private applyUploadResults(fileMap: Map<string, UploadFileResult>) {
    this.uploadQueue = this.uploadQueue.map(item => {
      if (item.status !== 'uploading' && item.status !== 'processing') return item;
      const result = fileMap.get(item.file.name.toLowerCase());
      if (!result) {
        return { ...item, status: 'failed', progress: 100, message: 'No result returned for this file.' };
      }
      return {
        ...item,
        status: result.status === 'completed' ? 'completed' : 'failed',
        progress: 100,
        message: result.message
      };
    });
  }

  private refreshResultsAfterUpload() {
    this.api.getResults(this.jobId, 1, 50).subscribe({
      next: latest => {
        const selectedId = this.selectedCandidate?.candidateId ?? null;
        this.results = latest;

        if (selectedId !== null) {
          this.selectedCandidate = latest.results.find(r => r.candidateId === selectedId) ?? null;
        }

        this.isUploading = false;
        this.uploadGlobalProgress = 100;
      },
      error: () => {
        this.isUploading = false;
        this.uploadError = 'Upload finished, but failed to refresh ranking. Please reload results.';
      }
    });
  }

  selectCandidate(candidate: CandidateResult) {
    this.selectedCandidate = this.selectedCandidate?.candidateId === candidate.candidateId
      ? null
      : candidate;
  }

  closeDetail() {
    this.selectedCandidate = null;
  }

  openDescriptionModal() {
    if (!this.results?.jobDescription?.trim()) return;
    this.lastFocusedBeforeDescriptionModal = document.activeElement as HTMLElement | null;
    this.isDescriptionModalOpen = true;
    queueMicrotask(() => this.focusFirstDescriptionModalElement());
  }

  closeDescriptionModal() {
    this.isDescriptionModalOpen = false;
    this.lastFocusedBeforeDescriptionModal?.focus();
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      if (this.isDescriptionModalOpen) {
        event.preventDefault();
        this.closeDescriptionModal();
        return;
      }

      if (this.isUploadModalOpen && !this.isUploading) {
        event.preventDefault();
        this.closeUploadModal();
      }
      return;
    }

    if (event.key === 'Tab' && this.isDescriptionModalOpen) {
      this.trapDescriptionModalFocus(event);
    }
  }

  private focusFirstDescriptionModalElement() {
    const focusable = this.getDescriptionModalFocusableElements();
    if (focusable.length > 0) {
      focusable[0].focus();
      return;
    }

    this.descriptionModalPanel?.nativeElement.focus();
  }

  private trapDescriptionModalFocus(event: KeyboardEvent) {
    const focusable = this.getDescriptionModalFocusableElements();
    if (focusable.length === 0) {
      event.preventDefault();
      this.descriptionModalPanel?.nativeElement.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement as HTMLElement | null;

    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
      return;
    }

    if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private getDescriptionModalFocusableElements(): HTMLElement[] {
    const root = this.descriptionModalPanel?.nativeElement;
    if (!root) return [];

    const selectors = [
      'button:not([disabled])',
      '[href]',
      'input:not([disabled])',
      'select:not([disabled])',
      'textarea:not([disabled])',
      '[tabindex]:not([tabindex="-1"])'
    ].join(', ');

    return Array.from(root.querySelectorAll<HTMLElement>(selectors))
      .filter(el => !el.hasAttribute('disabled') && el.tabIndex !== -1);
  }

  submitFeedback(candidate: CandidateResult, type: 'Approved' | 'Rejected') {
    if (this.feedbackLoading[candidate.candidateId]) return;

    const isSame = candidate.feedbackType === type;
    this.feedbackLoading[candidate.candidateId] = true;

    const request$: ReturnType<typeof this.api.removeFeedback> =
      isSame
        ? this.api.removeFeedback(candidate.candidateId, this.jobId)
        : (this.api.submitFeedback({ candidateId: candidate.candidateId, jobId: this.jobId, type }) as any);

    request$.subscribe({
      next: () => {
        candidate.feedbackType = isSame ? null : type;
        if (this.selectedCandidate?.candidateId === candidate.candidateId) {
          this.selectedCandidate = { ...candidate };
        }
        this.feedbackLoading[candidate.candidateId] = false;
      },
      error: () => {
        this.feedbackLoading[candidate.candidateId] = false;
      }
    });
  }

  getScoreColor(score: number): string {
    if (score >= 80) return '#22c55e';
    if (score >= 60) return '#f59e0b';
    return '#ef4444';
  }

  getScoreLabel(score: number): string {
    if (score >= 80) return 'Strong Match';
    if (score >= 60) return 'Potential';
    return 'Weak Match';
  }

  getRankBadgeClass(rank: number): string {
    if (rank === 1) return 'rank-gold';
    if (rank === 2) return 'rank-silver';
    if (rank === 3) return 'rank-bronze';
    return 'rank-default';
  }

  coreMatchPct(c: CandidateResult): number {
    if (!c.totalCoreKeywords) return 0;
    return Math.round((c.coreMatchedKeywords.length / c.totalCoreKeywords) * 100);
  }

  secondaryMatchPct(c: CandidateResult): number {
    if (!c.totalSecondaryKeywords) return 0;
    return Math.round((c.secondaryMatchedKeywords.length / c.totalSecondaryKeywords) * 100);
  }

  get approvedCount(): number {
    return this.results?.results.filter(r => r.feedbackType === 'Approved').length ?? 0;
  }

  get rejectedCount(): number {
    return this.results?.results.filter(r => r.feedbackType === 'Rejected').length ?? 0;
  }

  get pendingCount(): number {
    return this.results?.results.filter(r => !r.feedbackType).length ?? 0;
  }

  get hasQueuedUploads(): boolean {
    return this.uploadQueue.some(i => i.status === 'queued');
  }

  trackByCandidate(_: number, c: CandidateResult) {
    return c.candidateId;
  }
}

type UploadItemStatus = 'queued' | 'uploading' | 'processing' | 'completed' | 'failed';

interface UploadQueueItem {
  file: File;
  status: UploadItemStatus;
  progress: number;
  message: string;
}
