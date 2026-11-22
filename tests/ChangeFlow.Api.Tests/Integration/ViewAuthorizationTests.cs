using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChangeFlow.Api.Controllers;
using ChangeFlow.Api.Data;
using ChangeFlow.Api.Models;

namespace ChangeFlow.Api.Tests.Integration;

public sealed class ViewAuthorizationTests : IClassFixture<ChangeFlowWebApplicationFactory>
{
    private readonly ChangeFlowWebApplicationFactory _factory;

    public ViewAuthorizationTests(ChangeFlowWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Reviewer_can_view_full_request_but_readonly_user_cannot_modify_unrelated_request()
    {
        var requester = _factory.CreateClient();
        var requesterToken = await IssueTokenAsync(requester, DevelopmentSeed.RequesterOnlyId);
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);

        var created = await requester.PostAsJsonAsync("/change-requests", new
            { title = "Visibility", description = "Full details", reason = "Original reason" });
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();

        var requesterReviewSubmit = await requester.PostAsJsonAsync($"/change-requests/{request!.Id}/submit",
            new { concurrencyToken = request.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, requesterReviewSubmit.StatusCode);

        var reviewer = _factory.CreateClient();
        var reviewerToken = await IssueTokenAsync(reviewer, DevelopmentSeed.ReviewerOnlyId);
        reviewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reviewerToken);

        var get = await reviewer.GetAsync($"/change-requests/{request.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var view = await get.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.Equal(request.Title, view!.Title);
        Assert.Equal(request.Description, view.Description);
        Assert.Equal(request.Reason, view.Reason);

        var edit = await reviewer.PutAsJsonAsync($"/change-requests/{request.Id}", new
        {
            title = "Unauthorized",
            description = "Unauthorized",
            reason = "Unauthorized",
            concurrencyToken = request.ConcurrencyToken
        });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
    }

    private static async Task<string> IssueTokenAsync(HttpClient client, Guid userId)
    {
        var response = await client.PostAsJsonAsync("/development/tokens", new { userId });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>();
        return payload!.AccessToken;
    }
}
