using RoleFit.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<LocalStore>();
builder.Services.AddSingleton<DocumentReader>();
builder.Services.AddSingleton<AlignmentEngine>();
builder.Services.AddSingleton<ResumeProfileParser>();
builder.Services.AddSingleton<ProfileDraftStore>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ResumeArchiveStore>();
builder.Services.AddHttpClient("groq", client => client.Timeout = TimeSpan.FromSeconds(110));
builder.Services.AddScoped<GroqProfileService>();

// This development MVP listens only on 127.0.0.1; keep real resumes local.
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}
app.MapControllers();
app.Run();
