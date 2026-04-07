using Resume.Api.DTOs;

namespace Resume.Api.Services;

public interface IJobCandidateUploadService
{
    Task<CandidateUploadResponse> UploadAsync(int jobId, List<IFormFile> files);
}

