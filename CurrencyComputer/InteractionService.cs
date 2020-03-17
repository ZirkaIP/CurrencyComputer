using CurrencyComputer.Core;
using CurrencyComputer.Engine.Antlr;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CurrencyComputer
{
    public sealed class InteractionService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<InteractionService> _logger;

        public InteractionService(
            IConfiguration configuration,
            ILogger<InteractionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var computer = CreateComputer();
            while (true)
            {
                Console.Write("Enter function, please: \t");

                try
                {
                    var input = Console.ReadLine()?.Trim();
                    var result = computer.Compute(input);
                    Console.WriteLine($"{input} = {result.Value},{result.Currency}.");
                }
                catch (SyntaxException sEx)
                {
                    _logger.LogWarning(sEx.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception was thrown.");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        private IConversionComputer CreateComputer()
        {
            var costs = new Dictionary<string, Dictionary<string, decimal>>();
            _configuration.GetSection("conversionCosts").Bind(costs);

            var conventions = new Dictionary<string, string>();
            _configuration.GetSection("conversionToCurrencyConventions").Bind(conventions);

            return new Computer(costs, conventions, _logger);
        }
    }
}
