using ChangeFlow.Api.Data;
using ChangeFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Api.Services;

public sealed class ChangeRequestWorkflow(
    ChangeFlowDbContext db,
    SqliteImmediateTransaction transactions,
    IServiceScopeFactory scopeFactory)
{
    private static ChangeRequestResponse ToResponse(ChangeRequest request) => new(
        request.Id,
        request.Title,
        request.Description,
        request.Reason,
        request.LastChangeRequestComment,
        request.RequesterId,
        request.Status,
        request.ConcurrencyToken);

    public async Task<ChangeRequestResponse> CreateAsync(Guid callerId, CreateChangeRequestRequest input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Description) ||
            string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ChangeFlowProblemException(400, "MalformedInput", "Malformed input", "Title, description, and reason are required.");
        }

        return await transactions.ExecuteAsync(scopeFactory, async txDb =>
        {
            if (!await HasActivePermissionAsync(txDb, callerId, PermissionCapability.Requester, cancellationToken))
                throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "The caller does not have Requester permission.");

            var request = new ChangeRequest
            {
                Id = Guid.NewGuid(),
                Title = input.Title.Trim(),
                Description = input.Description.Trim(),
                Reason = input.Reason.Trim(),
                RequesterId = callerId,
                Status = ChangeRequestStatus.Draft,
                ConcurrencyToken = Guid.NewGuid()
            };

            txDb.ChangeRequests.Add(request);
            await txDb.SaveChangesAsync(cancellationToken);
            txDb.Entry(request).State = EntityState.Detached;
            return ToResponse(request);
        }, cancellationToken);
    }

    public async Task<ChangeRequestResponse?> GetAsync(Guid callerId, Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await db.ChangeRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
            throw new ChangeFlowProblemException(404, "NotFound", "Not found", "The Change Request was not found or is not viewable.");

        var requesterOrReviewer = request.RequesterId == callerId ||
            (request.Status == ChangeRequestStatus.PendingReview &&
             await HasActivePermissionAsync(db, callerId, PermissionCapability.Reviewer, cancellationToken));

        if (!requesterOrReviewer)
            throw new ChangeFlowProblemException(404, "NotFound", "Not found", "The Change Request was not found or is not viewable.");

        return ToResponse(request);
    }

    public async Task<ChangeRequestResponse> EditAsync(Guid callerId, Guid requestId,
        EditChangeRequestRequest input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Description) ||
            string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ChangeFlowProblemException(400, "MalformedInput", "Malformed input", "Title, description, and reason are required.");
        }

        return await transactions.ExecuteAsync(scopeFactory, async txDb =>
        {
            if (!await HasActivePermissionAsync(txDb, callerId, PermissionCapability.Requester, cancellationToken))
                throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "The caller does not have Requester permission.");

            var request = await txDb.ChangeRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
                ?? throw new ChangeFlowProblemException(404, "NotFound", "Not found", "The Change Request was not found.");

            if (request.RequesterId != callerId)
                throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "Only the Requester who owns this request may edit it.");

            if (request.Status is not (ChangeRequestStatus.Draft or ChangeRequestStatus.ChangesRequested))
                throw new ChangeFlowProblemException(409, "InvalidState", "Invalid state", "The Change Request cannot be edited in its current state.");

            if (request.ConcurrencyToken != input.ConcurrencyToken)
                throw new ChangeFlowProblemException(409, "ConcurrencyConflict", "Concurrency conflict", "The Change Request was modified after it was retrieved.");

            request.Title = input.Title.Trim();
            request.Description = input.Description.Trim();
            request.Reason = input.Reason.Trim();
            request.ConcurrencyToken = Guid.NewGuid();

            await txDb.SaveChangesAsync(cancellationToken);
            return ToResponse(request);
        }, cancellationToken);
    }

    public async Task<ChangeRequestResponse> SubmitAsync(Guid callerId, Guid requestId,
        MutationRequest input, CancellationToken cancellationToken)
    {
        return await MutateAsync(callerId, requestId, input.ConcurrencyToken, cancellationToken,
            async (request, txDb) =>
            {
                if (!await HasActivePermissionAsync(txDb, callerId, PermissionCapability.Requester, cancellationToken))
                    throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "The caller does not have Requester permission.");

                if (request.RequesterId != callerId)
                    throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "Only the Requester who owns this request may submit it.");

                if (request.Status is not (ChangeRequestStatus.Draft or ChangeRequestStatus.ChangesRequested))
                    throw new ChangeFlowProblemException(409, "InvalidState", "Invalid state", "The Change Request cannot be submitted in its current state.");

                request.Status = ChangeRequestStatus.PendingReview;
            });
    }

    private async Task<ChangeRequestResponse> MutateAsync(Guid callerId, Guid requestId, Guid concurrencyToken,
        CancellationToken cancellationToken, Func<ChangeRequest, ChangeFlowDbContext, Task> mutation)
    {
        return await transactions.ExecuteAsync(scopeFactory, async txDb =>
        {
            var request = await txDb.ChangeRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
                ?? throw new ChangeFlowProblemException(404, "NotFound", "Not found", "The Change Request was not found.");

            if (request.ConcurrencyToken != concurrencyToken)
                throw new ChangeFlowProblemException(409, "ConcurrencyConflict", "Concurrency conflict", "The Change Request was modified after it was retrieved.");

            await mutation(request, txDb);
            request.ConcurrencyToken = Guid.NewGuid();
            await txDb.SaveChangesAsync(cancellationToken);
            return ToResponse(request);
        }, cancellationToken);
    }

    public async Task<ChangeRequestResponse> ApproveAsync(Guid callerId, Guid requestId,
        MutationRequest input, CancellationToken cancellationToken)
    {
        return await MutateAsync(callerId, requestId, input.ConcurrencyToken, cancellationToken,
            async (request, txDb) =>
            {
                if (!await HasActivePermissionAsync(txDb, callerId, PermissionCapability.Reviewer, cancellationToken))
                    throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "The caller does not have Reviewer permission.");

                if (request.RequesterId == callerId)
                    throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "The Requester cannot approve their own Change Request.");

                if (request.Status != ChangeRequestStatus.PendingReview)
                    throw new ChangeFlowProblemException(409, "InvalidState", "Invalid state", "Only PendingReview requests can be approved.");

                request.Status = ChangeRequestStatus.Approved;
            });
    }

    public async Task<ChangeRequestResponse> RequestChangesAsync(Guid callerId, Guid requestId,
        RequestChangesRequest input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Comment))
            throw new ChangeFlowProblemException(400, "MissingOrEmptyChangeComment", "Missing or empty change-request comment", "A non-empty comment is required.");

        return await MutateAsync(callerId, requestId, input.ConcurrencyToken, cancellationToken,
            async (request, txDb) =>
            {
                if (!await HasActivePermissionAsync(txDb, callerId, PermissionCapability.Reviewer, cancellationToken))
                    throw new ChangeFlowProblemException(403, "Forbidden", "Forbidden", "The caller does not have Reviewer permission.");

                if (request.Status != ChangeRequestStatus.PendingReview)
                    throw new ChangeFlowProblemException(409, "InvalidState", "Invalid state", "Only PendingReview requests can be returned for changes.");

                request.LastChangeRequestComment = input.Comment.Trim();
                request.Status = ChangeRequestStatus.ChangesRequested;
            });
    }

    private static async Task<bool> HasActivePermissionAsync(ChangeFlowDbContext db, Guid userId,
        PermissionCapability capability, CancellationToken cancellationToken)
    {
        return await db.Permissions.AnyAsync(x => x.UserId == userId && x.Capability == capability && x.RevokedAt == null,
            cancellationToken);
    }

}
