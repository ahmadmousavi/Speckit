using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChangeFlow.Api.Controllers;
using ChangeFlow.Api.Data;
using ChangeFlow.Api.Models;

namespace ChangeFlow.Api.Tests.Integration;

public sealed class ConcurrencyAndStateTests : IClassFixture<ChangeFlowWebApplicationFactory>
{
    private readonly ChangeFlowWebApplicationFactory _factory;

    public ConcurrencyAndStateTests(ChangeFlowWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Stale_concurrency_token_is_rejected()
    {
        var client = _factory.CreateClient();
        var token = await IssueTokenAsync(client, DevelopmentSeed.RequesterOnlyId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync("/change-requests", new
            { title = "Concurrent", description = "Test", reason = "Test" });
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var first = await client.PutAsJsonAsync($"/change-requests/{request!.Id}", new
        {
            title = "First",
            description = "First",
            reason = "First",
            concurrencyToken = request.ConcurrencyToken
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await client.PutAsJsonAsync($"/change-requests/{request.Id}", new
        {
            title = "Stale",
            description = "Stale",
            reason = "Stale",
            concurrencyToken = request.ConcurrencyToken
        });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("ConcurrencyConflict", await stale.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unsupported_submit_from_Approved_is_InvalidState()
    {
        var client = _factory.CreateClient();
        var token = await IssueTokenAsync(client, DevelopmentSeed.RequesterOnlyId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync("/change-requests", new
            { title = "Approved", description = "Test", reason = "Test" });
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        request = await SubmitAndApproveAsync(client, request!);
        var resubmit = await client.PostAsJsonAsync($"/change-requests/{request.Id}/submit",
            new { concurrencyToken = request.ConcurrencyToken });

        Assert.Equal(HttpStatusCode.Conflict, resubmit.StatusCode);
        Assert.Contains("InvalidState", await resubmit.Content.ReadAsStringAsync());
    }

    private async Task<ChangeRequestResponse> SubmitAndApproveAsync(HttpClient requesterClient,
        ChangeRequestResponse request)
    {
        var submitted = await requesterClient.PostAsJsonAsync($"/change-requests/{request.Id}/submit",
            new { concurrencyToken = request.ConcurrencyToken });
        var pending = await submitted.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var reviewerClient = _factory.CreateClient();
        var reviewerToken = await IssueTokenAsync(reviewerClient, DevelopmentSeed.ReviewerOnlyId);
        reviewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reviewerToken);
        var approved = await reviewerClient.PostAsJsonAsync($"/change-requests/{request.Id}/approve",
            new { concurrencyToken = pending!.ConcurrencyToken });
        return (await approved.Content.ReadFromJsonAsync<ChangeRequestResponse>())!;
    }

    private static async Task<string> IssueTokenAsync(HttpClient client, Guid userId)
    {
        var response = await client.PostAsJsonAsync("/development/tokens", new { userId });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>();
        return payload!.AccessToken;
    }
}
