using System.Security.Claims;
using ChangeFlow.Api.Models;
using ChangeFlow.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChangeFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("change-requests")]
public sealed class ChangeRequestsController(ChangeRequestWorkflow workflow) : ControllerBase
{
    private Guid CallerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new ChangeFlowProblemException(401, "Unauthorized", "Unauthorized", "A valid subject is required."));

    [HttpPost]
    public async Task<ActionResult<ChangeRequestResponse>> Create([FromBody] CreateChangeRequestRequest request,
        CancellationToken cancellationToken)
    {
        var created = await workflow.CreateAsync(CallerId, request, cancellationToken);
        return Created($"/change-requests/{created.Id}", created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChangeRequestResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var response = await workflow.GetAsync(CallerId, id, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ChangeRequestResponse>> Edit(Guid id, [FromBody] EditChangeRequestRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workflow.EditAsync(CallerId, id, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ChangeRequestResponse>> Submit(Guid id, [FromBody] MutationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workflow.SubmitAsync(CallerId, id, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ChangeRequestResponse>> Approve(Guid id, [FromBody] MutationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workflow.ApproveAsync(CallerId, id, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/request-changes")]
    public async Task<ActionResult<ChangeRequestResponse>> RequestChanges(Guid id,
        [FromBody] RequestChangesRequest request, CancellationToken cancellationToken)
    {
        var response = await workflow.RequestChangesAsync(CallerId, id, request, cancellationToken);
        return Ok(response);
    }
}
