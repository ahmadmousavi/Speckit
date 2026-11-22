using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChangeFlow.Api.Data;
using ChangeFlow.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ChangeFlow.Api.Controllers;

[ApiController]
[Route("development/tokens")]
[AllowAnonymous]
public sealed class DevelopmentTokensController(IConfiguration configuration, ChangeFlowDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> IssueAsync([FromBody] DevelopmentTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            return Problem(statusCode: 400, title: "Malformed input", detail: "A valid userId is required.",
                type: "https://changeflow.local/problems/malformedinput");

        if (!await db.UserIdentities.AnyAsync(x => x.Id == request.UserId, cancellationToken))
            return NotFound();

        var key = configuration["DevelopmentJwt:SigningKey"]
            ?? throw new InvalidOperationException("DevelopmentJwt:SigningKey must be configured.");

        var issuer = configuration["DevelopmentJwt:Issuer"] ?? "ChangeFlow.Development";
        var audience = configuration["DevelopmentJwt:Audience"] ?? "ChangeFlow.Api";
        var expires = DateTime.UtcNow.AddMinutes(30);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) },
            expires: expires,
            signingCredentials: credentials);

        return Ok(new DevelopmentTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expires));
    }
}

public sealed record DevelopmentTokenRequest(Guid UserId);
public sealed record DevelopmentTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
