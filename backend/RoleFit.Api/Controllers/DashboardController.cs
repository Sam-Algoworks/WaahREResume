using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(LocalStore store) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(
        await store.ReadAsync(data => new
        {
            totalJobs = data.Jobs.Count,
            totalAnalyses = data.Analyses.Count,
            uniqueCandidates = data.Analyses.Select(a => a.CandidateName.Trim().ToUpperInvariant()).Distinct().Count(),
            averageScore = data.Analyses.Count == 0 ? 0 : (int)Math.Round(data.Analyses.Average(a => a.Score)),
            recent = data.Analyses.OrderByDescending(a => a.CreatedAt).Take(5).ToList()
        }, ct));
}
