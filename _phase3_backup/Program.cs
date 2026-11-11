using RoleFit.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<LocalStore>();
builder.Services.AddSingleton<DocumentReader>();
builder.Services.AddSingleton<AlignmentEngine>();

// This development MVP listens only on 127.0.0.1; keep real resumes local.
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}
app.MapControllers();
app.Run();
