import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

type Step = 1 | 2 | 3;

@Component({
  selector: 'app-upload',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './upload.component.html',
  styleUrl: './upload.component.css'
})
export class UploadComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  step: Step = 1;
  jobTitle = '';
  jobDescription = '';
  files: File[] = [];
  isDragging = false;
  isLoading = false;
  error = '';
  createdJobId: number | null = null;

  get canProceedStep1(): boolean {
    return this.jobTitle.trim().length > 2 && this.jobDescription.trim().length > 20;
  }

  get canProceedStep2(): boolean {
    return this.files.length > 0;
  }

  goToStep(s: Step) {
    this.step = s;
    this.error = '';
  }

  onFileDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragging = false;
    const dt = event.dataTransfer;
    if (dt) this.addFiles(Array.from(dt.files));
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragging = true;
  }

  onDragLeave() {
    this.isDragging = false;
  }

  onFileSelect(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) this.addFiles(Array.from(input.files));
  }

  private addFiles(incoming: File[]) {
    const allowed = incoming.filter(f =>
      ['.pdf', '.docx', '.txt'].some(ext => f.name.toLowerCase().endsWith(ext))
    );
    const newNames = new Set(this.files.map(f => f.name));
    this.files = [...this.files, ...allowed.filter(f => !newNames.has(f.name))];
  }

  removeFile(index: number) {
    this.files = this.files.filter((_, i) => i !== index);
  }

  getFileIcon(name: string): string {
    if (name.toLowerCase().endsWith('.pdf')) return 'pdf';
    if (name.toLowerCase().endsWith('.docx')) return 'word';
    return 'txt';
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  }

  async submitStep1() {
    if (!this.canProceedStep1) return;
    this.isLoading = true;
    this.error = '';
    try {
      const job = await this.api.createJob({
        title: this.jobTitle.trim(),
        description: this.jobDescription.trim()
      }).toPromise();
      this.createdJobId = job!.id;
      this.step = 2;
    } catch {
      this.error = 'Failed to create job. Is the API running?';
    } finally {
      this.isLoading = false;
    }
  }

  async submitStep2() {
    if (!this.canProceedStep2 || !this.createdJobId) return;
    this.isLoading = true;
    this.error = '';
    this.step = 3;
    try {
      await this.api.uploadCandidates(this.createdJobId, this.files).toPromise();
      setTimeout(() => {
        this.router.navigate(['/results', this.createdJobId]);
      }, 1200);
    } catch {
      this.error = 'Upload failed. Please try again.';
      this.step = 2;
    } finally {
      this.isLoading = false;
    }
  }
}
