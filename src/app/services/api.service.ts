import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEvent, HttpRequest } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Job, CreateJobRequest } from '../models/job.model';
import { ResultsResponse, FeedbackRequest, UploadResponse } from '../models/result.model';
import { ScreeningSummary } from '../models/screening.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  // Jobs
  getJobs(page = 1, pageSize = 20): Observable<Job[]> {
    return this.http.get<Job[]>(`${this.baseUrl}/jobs`, {
      params: { page: page.toString(), pageSize: pageSize.toString() }
    });
  }

  getScreeningsSummary(): Observable<ScreeningSummary[]> {
    return this.http.get<ScreeningSummary[]>(`${this.baseUrl}/jobs/summary`);
  }

  getJob(id: number): Observable<Job> {
    return this.http.get<Job>(`${this.baseUrl}/jobs/${id}`);
  }

  createJob(payload: CreateJobRequest): Observable<Job> {
    return this.http.post<Job>(`${this.baseUrl}/jobs`, payload);
  }

  deleteJob(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/jobs/${id}`);
  }

  // Candidates
  uploadCandidatesForExistingJob(jobId: number, files: File[]): Observable<HttpEvent<UploadResponse>> {
    const form = new FormData();
    files.forEach(f => form.append('files', f));

    const req = new HttpRequest(
      'POST',
      `${this.baseUrl}/jobs/${jobId}/candidates/upload`,
      form,
      {
        reportProgress: true,
        responseType: 'json'
      }
    );
    return this.http.request<UploadResponse>(req);
  }

  // Results
  getResults(jobId: number, page = 1, pageSize = 50): Observable<ResultsResponse> {
    return this.http.get<ResultsResponse>(`${this.baseUrl}/results/${jobId}`, {
      params: { page: page.toString(), pageSize: pageSize.toString() }
    });
  }

  // Feedback
  submitFeedback(payload: FeedbackRequest): Observable<{ message: string; type: string }> {
    return this.http.post<{ message: string; type: string }>(`${this.baseUrl}/feedback`, payload);
  }

  removeFeedback(candidateId: number, jobId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/feedback?candidateId=${candidateId}&jobId=${jobId}`);
  }
}
