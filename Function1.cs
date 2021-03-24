using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace VapaatTilatFunctionApp
{
    public static class Function1
    {
        [FunctionName("VapaatTilatFunktio")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ExecutionContext context, ILogger log)
        {

            IConfigurationRoot config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                    // This gives you access to your application settings in your local development environment
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    // This is what actually gets you the application settings in Azure
                        .AddEnvironmentVariables()
                        .Build();

            string appsettingvalue = config["appsettingkey"];



            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];
            string tyypit = req.Query["valinnat"];
            string paikkak = req.Query["paikkak"];
            string alkupvm = req.Query["alkupvm"];
            string paattyenpvm = req.Query["paattyenpvm"];
            string resurssihaku = req.Query["resurssihaku"];
            string ajankohta = req.Query["ajankohta"];

            IWilmaResurssiLaskenta resurssiLaskenta = new WilmaResurssiLaskenta(config);

            switch (resurssihaku)
            {
                case "ruokailijat":
                    return new OkObjectResult(resurssiLaskenta.CountRuokailijat(alkupvm, paattyenpvm));
                    
                case "ruokailjat2":
                    return new OkObjectResult(resurssiLaskenta.CountRuokailijat2(alkupvm, paattyenpvm));
                    
                default:
                    return new OkObjectResult(resurssiLaskenta.PopulateTilat(tyypit, alkupvm, paattyenpvm, paikkak, ajankohta, resurssihaku));

            }
        }
    }
}

