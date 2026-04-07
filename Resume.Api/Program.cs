using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.Services;
using Resume.Api.Services.NameExtraction;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                     ?? "Data Source=resume_yourself.db"));

builder.Services.AddScoped<ICvParserService, CvParserService>();
builder.Services.AddScoped<INameExtractionService, NameExtractionService>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IJobCandidateUploadService, JobCandidateUploadService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
