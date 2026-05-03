namespace Resume.Api.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verificationLink, CancellationToken cancellationToken = default);
}
