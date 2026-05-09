// Generated from backend OpenAPI schema.
// To regenerate:
// npm run generate:api-types

export interface CreateJobRequest {
  title: string;
  description: string;
}

export interface JobResponse {
  id: number;
  title: string;
  description: string;
  createdAt: string;
}

export interface JobSummaryResponse {
  id: number;
  title: string;
  createdAt: string;
  totalCandidates: number;
  averageScore: number;
  topScore: number;
  approvedCount: number;
  rejectedCount: number;
}

export interface CandidateResultResponse {
  candidateId: number;
  scoreId: number;
  name: string;
  email: string;
  fileName: string;
  score: number;
  rank: number;
  coreMatchedKeywords: string[];
  coreMissingKeywords: string[];
  secondaryMatchedKeywords: string[];
  secondaryMissingKeywords: string[];
  hardFilters: string[];
  scoreReasons: string[];
  totalCoreKeywords: number;
  totalSecondaryKeywords: number;
  feedbackType: 'Approved' | 'Rejected' | null;
}

export interface ResultsResponse {
  jobId: number;
  jobTitle: string;
  jobDescription: string;
  totalCandidates: number;
  averageScore: number;
  results: CandidateResultResponse[];
}

export interface FeedbackRequest {
  candidateId: number;
  jobId: number;
  type: 'Approved' | 'Rejected';
}

export interface CandidateUploadFileResult {
  fileName: string;
  status: 'completed' | 'failed';
  message: string;
  candidateId: number | null;
  score: number | null;
  rank: number | null;
}

export interface CandidateUploadResponse {
  jobId: number;
  processedCount: number;
  failedCount: number;
  files: CandidateUploadFileResult[];
}

export type UploadResponse = CandidateUploadResponse;

export interface LoginRequest {
  email: string;
  password: string;
}

// Refresh token is set as an httpOnly cookie by the backend and never exposed in the body.
export interface AccessTokenResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  email: string;
  roles: string[];
}
