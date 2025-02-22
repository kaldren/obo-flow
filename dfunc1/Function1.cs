using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace dfunc1
{
    [Authorize]
    public class Function1
    {
        private readonly IEntraIDJwtBearerValidation _entra;

        public Function1(IEntraIDJwtBearerValidation entra)
        {
            this._entra = entra;
        }

        [Function(nameof(Function1))]
        public async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(Function1));
            var outputs = new List<string>();

            outputs.Add(await context.CallActivityAsync<string>(nameof(GetAllBlobs)));

            return outputs;
        }

        [Function(nameof(GetAllBlobs))]
        public async static Task<string> GetAllBlobs([ActivityTrigger] FunctionContext executionContext)
        {
            string storageAccountName = "stodemoobo";
            string containerName = "xyz";
            string blobServiceUri = $"https://{storageAccountName}.blob.core.windows.net/";

            var logger = executionContext.GetLogger(nameof(GetAllBlobs));
            var blobNames = new List<string>();

            try
            {
                var blobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), new DefaultAzureCredential());
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

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(Function1));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
