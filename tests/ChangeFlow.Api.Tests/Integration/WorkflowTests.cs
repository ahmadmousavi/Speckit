using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChangeFlow.Api.Controllers;
using ChangeFlow.Api.Data;
using ChangeFlow.Api.Models;

namespace ChangeFlow.Api.Tests.Integration;

public sealed class WorkflowTests : IClassFixture<ChangeFlowWebApplicationFactory>
{
    private readonly ChangeFlowWebApplicationFactory _factory;

    public WorkflowTests(ChangeFlowWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Reviewer_can_approve_without_self_approval_and_request_changes_with_comment()
    {
        var requesterClient = _factory.CreateClient();
        var requesterToken = await IssueTokenAsync(requesterClient, DevelopmentSeed.RequesterOnlyId);
        requesterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);

        var created = await requesterClient.PostAsJsonAsync("/change-requests", new
        {
            title = "Approval test",
            description = "Test",
            reason = "Original reason"
        });
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var submitted = await requesterClient.PostAsJsonAsync($"/change-requests/{request!.Id}/submit",
            new { concurrencyToken = request.ConcurrencyToken });
        var pending = await submitted.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var reviewerClient = _factory.CreateClient();
        var reviewerToken = await IssueTokenAsync(reviewerClient, DevelopmentSeed.ReviewerOnlyId);
        reviewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reviewerToken);

        var changes = await reviewerClient.PostAsJsonAsync($"/change-requests/{request.Id}/request-changes",
            new { concurrencyToken = pending!.ConcurrencyToken, comment = "Add rollback details" });
        Assert.Equal(HttpStatusCode.OK, changes.StatusCode);
        var changesRequest = await changes.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.Equal("Original reason", changesRequest!.Reason);
        Assert.Equal("Add rollback details", changesRequest.LastChangeRequestComment);

        var resubmitted = await requesterClient.PostAsJsonAsync($"/change-requests/{request.Id}/submit",
            new { concurrencyToken = changesRequest.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);

        var approval = await reviewerClient.PostAsJsonAsync($"/change-requests/{request.Id}/approve",
            new { concurrencyToken = (await resubmitted.Content.ReadFromJsonAsync<ChangeRequestResponse>())!.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
    }

    [Fact]
    public async Task Request_changes_without_comment_is_rejected()
    {
        var requesterClient = _factory.CreateClient();
        var requesterToken = await IssueTokenAsync(requesterClient, DevelopmentSeed.RequesterOnlyId);
        requesterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var created = await requesterClient.PostAsJsonAsync("/change-requests", new { title = "T", description = "D", reason = "R" });
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        await requesterClient.PostAsJsonAsync($"/change-requests/{request!.Id}/submit", new { concurrencyToken = request.ConcurrencyToken });

        var reviewerClient = _factory.CreateClient();
        var reviewerToken = await IssueTokenAsync(reviewerClient, DevelopmentSeed.ReviewerOnlyId);
        reviewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reviewerToken);

        var before = await reviewerClient.GetAsync($"/change-requests/{request.Id}");
        var beforeRequest = await before.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var response = await reviewerClient.PostAsJsonAsync($"/change-requests/{request.Id}/request-changes",
            new { concurrencyToken = beforeRequest!.ConcurrencyToken, comment = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("MissingOrEmptyChangeComment", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mutating_approved_request_is_invalid_state()
    {
        var requesterClient = _factory.CreateClient();
        var requesterToken = await IssueTokenAsync(requesterClient, DevelopmentSeed.RequesterOnlyId);
        requesterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var created = await requesterClient.PostAsJsonAsync("/change-requests", new { title = "T", description = "D", reason = "R" });
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var submitted = await requesterClient.PostAsJsonAsync($"/change-requests/{request!.Id}/submit",
            new { concurrencyToken = request.ConcurrencyToken });
        var pending = await submitted.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var reviewerClient = _factory.CreateClient();
        var reviewerToken = await IssueTokenAsync(reviewerClient, DevelopmentSeed.ReviewerOnlyId);
        reviewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reviewerToken);
        var approved = await reviewerClient.PostAsJsonAsync($"/change-requests/{request.Id}/approve",
            new { concurrencyToken = pending!.ConcurrencyToken });
        var approvedRequest = await approved.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var edit = await requesterClient.PutAsJsonAsync($"/change-requests/{request.Id}", new
        {
            title = "Changed",
            description = "Changed",
            reason = "Changed",
            concurrencyToken = approvedRequest!.ConcurrencyToken
        });

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Contains("InvalidState", await edit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dual_role_user_cannot_approve_own_request()
    {
        var dualRoleClient = _factory.CreateClient();
        var dualRoleToken = await IssueTokenAsync(dualRoleClient, DevelopmentSeed.DualRoleId);
        dualRoleClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dualRoleToken);

        var created = await dualRoleClient.PostAsJsonAsync("/change-requests", new
        {
            title = "Dual role self approval",
            description = "Test",
            reason = "Test"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var submitted = await dualRoleClient.PostAsJsonAsync($"/change-requests/{request!.Id}/submit",
            new { concurrencyToken = request.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var pending = await submitted.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var approval = await dualRoleClient.PostAsJsonAsync($"/change-requests/{request.Id}/approve",
            new { concurrencyToken = pending!.ConcurrencyToken });

        Assert.Equal(HttpStatusCode.Forbidden, approval.StatusCode);
    }

    private static async Task<string> IssueTokenAsync(HttpClient client, Guid userId)
    {
        var response = await client.PostAsJsonAsync("/development/tokens", new { userId });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>();
        return payload!.AccessToken;
    }
}
