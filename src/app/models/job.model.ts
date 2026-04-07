export interface Job {
  id: number;
  title: string;
  description: string;
  createdAt: string;
}

export interface CreateJobRequest {
  title: string;
  description: string;
}
