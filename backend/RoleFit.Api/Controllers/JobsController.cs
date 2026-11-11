using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Models;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(LocalStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(
        await store.ReadAsync(data => data.Jobs.OrderByDescending(j => j.CreatedAt).ToList(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 120
            || string.IsNullOrWhiteSpace(request.Description) || request.Description.Length < 30
            || request.Description.Length > 50_000)
            return BadRequest(new ApiError("Enter a title (max 120 characters) and a description of 30-50,000 characters."));
        var job = new JobRecord { Title = request.Title.Trim(), Description = request.Description.Trim() };
        await store.UpdateAsync(data => { data.Jobs.Add(job); return job; }, ct);
        return Created($"/api/jobs/{job.Id}", job);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var job = await store.ReadAsync(data => data.Jobs.FirstOrDefault(j => j.Id == id), ct);
        return job is null ? NotFound(new ApiError("Job not found.")) : Ok(job);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var removed = await store.UpdateAsync(data => data.Jobs.RemoveAll(j => j.Id == id) > 0, ct);
        return removed ? NoContent() : NotFound(new ApiError("Job not found."));
    }
}
