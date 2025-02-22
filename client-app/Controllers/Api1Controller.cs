using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using Microsoft.Identity.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace client_app.Controllers;

[Authorize]
public class Api1Controller : Controller
{

    private readonly IDownstreamApi _downstreamApi;

    public Api1Controller(IDownstreamApi downstreamApi)
    {
        _downstreamApi = downstreamApi;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
    public async Task<IActionResult> CallKeyVault()
    {
        using var response = await _downstreamApi.CallApiForUserAsync("DownstreamApi", opt =>
        {
            opt.RelativePath = "/api1/keyvault";
        }).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var apiResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            ViewData["ApiResult"] = apiResult;
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"Invalid status code in the HttpResponseMessage: {response.StatusCode}: {error}");
        };

        return View();
    }

    [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
    public async Task<IActionResult> CallTokenExpiration()
    {
        using var response = await _downstreamApi.CallApiForUserAsync("DownstreamApi", opt =>
        {
            opt.RelativePath = "/api1/token";
        }).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var apiResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            ViewData["ApiResult"] = apiResult;
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"Invalid status code in the HttpResponseMessage: {response.StatusCode}: {error}");
        };

        return View();
    }
}
