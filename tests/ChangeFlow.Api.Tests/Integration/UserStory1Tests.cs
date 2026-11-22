using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChangeFlow.Api.Controllers;
using ChangeFlow.Api.Data;
using ChangeFlow.Api.Models;

namespace ChangeFlow.Api.Tests.Integration;

public sealed class UserStory1Tests : IClassFixture<ChangeFlowWebApplicationFactory>
{
    private readonly ChangeFlowWebApplicationFactory _factory;

    public UserStory1Tests(ChangeFlowWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Requester_can_create_edit_submit_and_receive_location()
    {
        var client = _factory.CreateClient();
        var token = await IssueTokenAsync(client, DevelopmentSeed.RequesterOnlyId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync("/change-requests", new
        {
            title = "Upgrade runtime",
            description = "Move to supported runtime",
            reason = "Security support"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(request);
        Assert.Equal(ChangeRequestStatus.Draft, request.Status);

        var edited = await client.PutAsJsonAsync($"/change-requests/{request.Id}", new
        {
            title = "Upgrade runtime safely",
            description = request.Description,
            reason = "Updated security support",
            concurrencyToken = request.ConcurrencyToken
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var editedRequest = await edited.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(editedRequest);

        var submitted = await client.PostAsJsonAsync($"/change-requests/{request.Id}/submit",
            new { concurrencyToken = editedRequest.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var submittedRequest = await submitted.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.Equal(ChangeRequestStatus.PendingReview, submittedRequest!.Status);
    }

    [Fact]
    public async Task Draft_delete_attempt_is_rejected_and_request_is_preserved()
    {
        var client = _factory.CreateClient();
        var token = await IssueTokenAsync(client, DevelopmentSeed.RequesterOnlyId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync("/change-requests", new
        {
            title = "Draft",
            description = "Description",
            reason = "Reason"
        });
        var request = await created.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(request);

        var delete = await client.DeleteAsync($"/change-requests/{request.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);

        var get = await client.GetAsync($"/change-requests/{request.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var preserved = await get.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.Equal(request.Id, preserved!.Id);
        Assert.Equal(ChangeRequestStatus.Draft, preserved.Status);
    }

    private static async Task<string> IssueTokenAsync(HttpClient client, Guid userId)
    {
        var response = await client.PostAsJsonAsync("/development/tokens", new { userId });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>();
        return payload!.AccessToken;
    }
}
