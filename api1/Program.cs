using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
var keyVaultUrl = builder.Configuration["KeyVault:Url"];
var tenantId = builder.Configuration["AzureAd:TenantId"];
var clientId = builder.Configuration["AzureAd:ClientId"];
var clientSecret = builder.Configuration["AzureAd:ClientSecret"];

// Add JWT Bearer Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Add Authorization policy that requires "api1.readwrite" scope
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Api1Scope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireScope("api1.readwrite");
    });
});

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API 1",
        Version = "v1",
        Description = "Minimal API that checks for api1.readwrite scope"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// Minimal GET endpoint that checks for "api1.readwrite" scope
app.MapGet("/hi", [Authorize(Policy = "Api1Scope")] () =>
{
    return Results.Ok(new { message = "Hi from the following scope: api1.readwrite" });
});

// Connect to Azure Key Vault (https://kv-obo.vault.azure.net/)

/// Endpoint: Access Key Vault using OBO flow
app.MapGet("/keyvault", [Authorize(Policy = "Api1Scope")] async (HttpContext context) =>
{
    var userToken = await context.GetTokenAsync("access_token");
    if (string.IsNullOrEmpty(userToken))
    {
        return Results.Unauthorized();
    }

    // ✅ Acquire Key Vault token using OBO flow
    var kvToken = await AcquireTokenOnBehalfOfUserAsync(userToken, tenantId, clientId, clientSecret);

    // ✅ Access Key Vault using the acquired token
    var secretClient = new SecretClient(
        new Uri(keyVaultUrl),
        new AccessTokenCredential(kvToken)
    );

    var secret = await secretClient.GetSecretAsync("my-secret");
    return Results.Ok(new { secret = secret.Value.Value });
});

app.Run();

/// ✅ Helper: Acquire token for Key Vault using OBO flow
async Task<string> AcquireTokenOnBehalfOfUserAsync(string userAccessToken, string tenantId, string clientId, string clientSecret)
{
var appConfidential = ConfidentialClientApplicationBuilder
    .Create(clientId)
    .WithClientSecret(clientSecret)
    .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
    .Build();

var userAssertion = new UserAssertion(userAccessToken);
var result = await appConfidential.AcquireTokenOnBehalfOf(new[] { "https://vault.azure.net/.default" }, userAssertion)
                                  .ExecuteAsync();

return result.AccessToken;
}

/// ✅ Custom TokenCredential for SecretClient
public class AccessTokenCredential : TokenCredential
{
    private readonly string _accessToken;

    public AccessTokenCredential(string accessToken)
    {
        _accessToken = accessToken;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new AccessToken(_accessToken, DateTimeOffset.UtcNow.AddHours(1));

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new ValueTask<AccessToken>(new AccessToken(_accessToken, DateTimeOffset.UtcNow.AddHours(1)));
}
