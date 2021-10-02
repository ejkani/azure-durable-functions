using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace InProccessFuncApp
{
    public static class ChainingOrchestrator
    {
        [FunctionName(nameof(ChainingOrchestratorFunction))]
        public static async Task<List<string>> ChainingOrchestratorFunction(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>(nameof(ChainingOrchestratorFunctionSayHello), "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(ChainingOrchestratorFunctionSayHello), "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(ChainingOrchestratorFunctionSayHello), "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName(nameof(ChainingOrchestratorFunctionSayHello))]
        public static string ChainingOrchestratorFunctionSayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName(nameof(ChainingOrchestratorFunctionHttpStart))]
        public static async Task<HttpResponseMessage> ChainingOrchestratorFunctionHttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(ChainingOrchestratorFunction), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}