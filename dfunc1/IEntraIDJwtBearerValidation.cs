using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace dfunc1;
public interface IEntraIDJwtBearerValidation
{
    public Task<TokenValidationResult?> ValidateTokenAsync(string? authorizationHeader);

    public string GetPreferredUserName(ClaimsIdentity claimsIdentity);

    public Task<OpenIdConnectConfiguration> GetOIDCWellknownConfiguration();

    public bool IsScopeValid(string scopeName, ClaimsIdentity claimsIdentity);
}