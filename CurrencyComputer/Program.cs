
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CurrencyComputer
{
    class Program
    {
        static Task Main()
        {
            return CreateHost().StartAsync();
        }

        private static IHost CreateHost()
        {
            var loggingConfiguration = new ConfigurationBuilder()
                .AddJsonFile("logging.settings.json")
                .Build();

            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostContext, configApp) =>
                    configApp
                        .AddConfiguration(loggingConfiguration)
                        .AddJsonFile("app.settings.json"))
                .ConfigureServices((hCtx, services) =>
                    services
                        .AddHostedService<InteractionService>()
                        .AddLogging(loggingBuilder =>
                        {
                            var logger = new LoggerConfiguration()
                                .ReadFrom.Configuration(loggingConfiguration)
                                .CreateLogger();

                            loggingBuilder
                                .ClearProviders()
                                .AddSerilog(logger, dispose: true);
                        }))
                .UseConsoleLifetime()
                .Build();
        }
    }
}
