export interface CandidateResult {
  candidateId: number;
  scoreId: number;
  name: string;
  email: string;
  fileName: string;
  score: number;
  rank: number;
  // Weighted breakdown
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
  totalCandidates: number;
  averageScore: number;
  results: CandidateResult[];
}

export interface FeedbackRequest {
  candidateId: number;
  jobId: number;
  type: 'Approved' | 'Rejected';
}

export interface UploadResponse {
  jobId: number;
  processedCount: number;
  failedCount: number;
  files: UploadFileResult[];
}

export interface UploadFileResult {
  fileName: string;
  status: 'completed' | 'failed';
  message: string;
  candidateId: number | null;
  score: number | null;
  rank: number | null;
}
