
using RoleFit.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Register API controllers
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Register application services
builder.Services.AddSingleton<LocalStore>();
builder.Services.AddSingleton<DocumentReader>();
builder.Services.AddSingleton<AlignmentEngine>();

// Resume Parser services
builder.Services.AddSingleton<ResumeProfileParser>();
builder.Services.AddSingleton<ProfileDraftStore>();

// Groq AI integration
builder.Services.AddHttpClient(
    "groq",
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(110);
    }
);

builder.Services.AddScoped<GroqProfileService>();

// Build the application
var app = builder.Build();

// Production error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// Map API endpoints
app.MapControllers();

// Keep the web server running
app.Run();
