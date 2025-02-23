using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Text.Json;

namespace dfunc1
{
    [Authorize]
    public class Function1
    {
        private readonly IEntraIDJwtBearerValidation _entra;
        private readonly IConfiguration _configuration;

        public Function1(IEntraIDJwtBearerValidation entra, IConfiguration configuration)
        {
            this._entra = entra;
            this._configuration = configuration;
        }

        [Function(nameof(Function1))]
        public async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            // Retrieve the token passed from the starter function
            var token = context.GetInput<string>();

            ILogger logger = context.CreateReplaySafeLogger(nameof(Function1));
            var outputs = new List<string>();

            outputs.Add(await context.CallActivityAsync<string>(nameof(GetAllBlobs), token));

            return outputs;
        }

        [Function(nameof(GetAllBlobs))]
        public async Task<string> GetAllBlobs([ActivityTrigger] string token, FunctionContext executionContext)
        {
            var storageToken = await AcquireTokenOnBehalfOfUserAsync(token, _configuration["AzureAd:TenantId"], _configuration["AzureAd:ClientId"], _configuration["AzureAd:ClientSecret"]);

            string storageAccountName = "stodemoobo";
            string containerName = "xyz";
            string blobServiceUri = $"https://{storageAccountName}.blob.core.windows.net/";

            var logger = executionContext.GetLogger(nameof(GetAllBlobs));
            var blobNames = new List<string>();

            try
            {
                var blobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), new AccessTokenCredential(storageToken));
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                logger.LogInformation($"Listing blobs in container '{containerName}':");

                await foreach (var blobItem in containerClient.GetBlobsAsync())
                {
                    logger.LogInformation($"- {blobItem.Name}");
                    blobNames.Add(blobItem.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred while listing blobs: {ex.Message}");
                throw;
            }

            var response = new
            {
                Blobs = blobNames
            };

            return JsonSerializer.Serialize(response);
        }

        [Function("Function1_HttpStart")]
        public async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            if (!req.Headers.TryGetValues("Authorization", out var authHeader))
            {
                throw new UnauthorizedAccessException();
            }

            ILogger logger = executionContext.GetLogger("SayHello");

            var tokenValidationResult = await _entra.ValidateTokenAsync(authHeader.First());

            if (tokenValidationResult is null)
            {
                throw new UnauthorizedAccessException();
            }

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(Function1),
                _entra.Token);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        async Task<string> AcquireTokenOnBehalfOfUserAsync(string userAccessToken, string tenantId, string clientId, string clientSecret)
        {
            var appConfidential = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

            var userAssertion = new UserAssertion(userAccessToken);
            var result = await appConfidential.AcquireTokenOnBehalfOf(new[] { "https://storage.azure.com/.default" }, userAssertion)
                                              .ExecuteAsync();

            return result.AccessToken;
        }

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

    }
}
