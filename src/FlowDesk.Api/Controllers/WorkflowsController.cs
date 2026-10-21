using System.Text.Json;
using FlowDesk.Api.Auth;
using FlowDesk.Domain;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController(AppDbContext db) : ControllerBase
{
    public record CreateWorkflowRequest(string Name, JsonElement Definition);

    [HttpPost]
    [Authorize(Policy = Policies.ManageWorkflows)]
    public async Task<IActionResult> Create(CreateWorkflowRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 200)
            return Problem(title: "Name eka characters 1 ta 200 ta athara wenna one", statusCode: StatusCodes.Status400BadRequest);
        if (req.Definition.ValueKind != JsonValueKind.Object)
            return Problem(title: "'definition' eka JSON object ekak wenna one", statusCode: StatusCodes.Status400BadRequest);
        var lastVersion = await db.WorkflowDefinitions
            .Where(x => x.Name == req.Name)
            .MaxAsync(x => (int?)x.Version) ?? 0;

        var wf = new WorkflowDefinition
        {
            Name = req.Name,
            DefinitionJson = req.Definition.GetRawText(),
            Version = lastVersion + 1
        };

        db.WorkflowDefinitions.Add(wf);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = wf.Id }, wf);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.WorkflowDefinitions.AsNoTracking()
            .OrderBy(x => x.Name).ThenBy(x => x.Version)
            .ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var wf = await db.WorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return wf is null ? NotFound() : Ok(wf);
    }
}