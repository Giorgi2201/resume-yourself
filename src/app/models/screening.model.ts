export interface ScreeningSummary {
  id: number;
  title: string;
  createdAt: string;
  totalCandidates: number;
  averageScore: number;
  topScore: number;
  approvedCount: number;
  rejectedCount: number;
}
