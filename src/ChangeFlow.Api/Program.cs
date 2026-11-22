using System.Text;
using ChangeFlow.Api.Data;
using ChangeFlow.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ChangeFlowExceptionHandler>();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("ChangeFlow") ?? "Data Source=changeflow.db";
builder.Services.AddDbContext<ChangeFlowDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<SqliteImmediateTransaction>();
builder.Services.AddScoped<ChangeRequestWorkflow>();
builder.Services.AddScoped<PermissionStore>();

var signingKey = builder.Configuration["DevelopmentJwt:SigningKey"] ?? throw new InvalidOperationException("DevelopmentJwt:SigningKey must be configured.");

var issuer = builder.Configuration["DevelopmentJwt:Issuer"] ?? "ChangeFlow.Development";
var audience = builder.Configuration["DevelopmentJwt:Audience"] ?? "ChangeFlow.Api";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey is null
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ChangeFlowDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DevelopmentSeed.SeedAsync(db);
    }
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
