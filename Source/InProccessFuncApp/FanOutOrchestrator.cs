using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace InProccessFuncApp
{
    public static class FanOutOrchestrator
    {
        [FunctionName(nameof(FanOutOrchestratorFunction))]
        public static async Task<List<string>> FanOutOrchestratorFunction(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var tasks = new List<Task<string>>();

            var outputs = new List<string>();

            // Do not await the tasks, but add them to the list
            tasks.Add(context.CallActivityAsync<string>($"{nameof(FanOutOrchestratorFunctionSayHello)}", "Tokyo"));
            tasks.Add(context.CallActivityAsync<string>($"{nameof(FanOutOrchestratorFunctionSayHello)}", "Seattle"));
            tasks.Add(context.CallActivityAsync<string>($"{nameof(FanOutOrchestratorFunctionSayHello)}", "London"));

            // Now fan-out
            await Task.WhenAll(tasks);

            return tasks.Select(x => x.Result).ToList();
        }

        [FunctionName(nameof(FanOutOrchestratorFunctionSayHello))]
        public static async Task<string> FanOutOrchestratorFunctionSayHello([ActivityTrigger] string name, ILogger log)
        {
            var random = new Random();
            var delay = random.Next(1, 10);

            await Task.Delay(TimeSpan.FromSeconds(delay));
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName(nameof(FanOutOrchestratorFunctionHttpStart))]
        public static async Task<HttpResponseMessage> FanOutOrchestratorFunctionHttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(FanOutOrchestratorFunction), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}