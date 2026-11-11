using System.Net;
using System.Threading.RateLimiting;
using RoleFit.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Local MVP safeguards. This is NOT a substitute for per-user authentication.
// Do not host applicants' resumes publicly until access controls and storage are upgraded.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 90,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
// SQL Server is opt-in. Without a connection string, the existing JSON storage continues to work.
var historyConnection = builder.Configuration.GetConnectionString("RoleFitHistory");
if (!string.IsNullOrWhiteSpace(historyConnection))
{
    builder.Services.AddDbContextFactory<HistoryDbContext>(options => options.UseSqlServer(historyConnection));
    builder.Services.AddSingleton<SqlHistoryRepository>();
}
builder.Services.AddSingleton<LocalStore>();
builder.Services.AddSingleton<DocumentReader>();
builder.Services.AddSingleton<AlignmentEngine>();
builder.Services.AddSingleton<ResumeProfileParser>();
builder.Services.AddSingleton<ProfileDraftStore>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ResumeArchiveStore>();
builder.Services.AddHttpClient("groq", client => client.Timeout = TimeSpan.FromSeconds(110));
builder.Services.AddScoped<GroqProfileService>();
builder.Services.AddScoped<JdRequirementsService>();

var app = builder.Build();
if (!string.IsNullOrWhiteSpace(historyConnection))
{
    // The import is non-destructive: existing JSON files remain in App_Data.
    // SQL table creation and first import complete before the app accepts requests.
    await app.Services.GetRequiredService<SqlHistoryRepository>().InitializeAsync(app.Lifetime.ApplicationStopping);
}

// FAIL CLOSED: authentication and durable, tenant-isolated storage are not
// implemented yet. Production must never expose resumes, drafts or evaluations.
if (!app.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "Waah Re Resume is not ready for public hosting. " +
        "Implement authenticated per-user authorization and durable private storage first.");

// Even when Development is accidentally bound to a public interface, deny
// non-loopback clients. Vite's local proxy connects through loopback.
app.Use(async (context, next) =>
{
    var remote = context.Connection.RemoteIpAddress;
    var isLocal = remote is not null &&
        (IPAddress.IsLoopback(remote) ||
         (remote.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(remote.MapToIPv4())));
    if (!isLocal)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});

app.UseRateLimiter();
app.MapControllers();
app.Run();
