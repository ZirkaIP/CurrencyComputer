using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using CurrencyComputer.Core;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;

namespace CurrencyComputer.Engine.Antlr
{
    public sealed partial class Computer : IConversionComputer
    {
        private readonly IDictionary<string, Dictionary<string, decimal>> _conversionsCost;
        private readonly IDictionary<string, string> _conversionToCurrencyConventions;

        private readonly ILogger _logger;

        public Computer(
            IDictionary<string, Dictionary<string, decimal>> conversionsCost,
            IDictionary<string, string> conversionToCurrencyConventions,
            ILogger logger)
        {
            _conversionsCost = conversionsCost;
            _conversionToCurrencyConventions = conversionToCurrencyConventions;
            _logger = logger;
        }

        public ComputeResult Compute(string input)
        {
            var streamInput = new AntlrInputStream(input);
            var lexer = new CurrencyComputerLexer(streamInput);

            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new ErrorListener());

            var tokens = new CommonTokenStream(lexer);

            var parser = new CurrencyComputerParser(tokens)
            {
                BuildParseTree = true
            };
            parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;

            var resultComputed = (Tuple<decimal, string>)new ComputerVisitor(
                    _conversionsCost,
                    _conversionToCurrencyConventions,
                    _logger)
                .VisitInput(parser.input());

            return new ComputeResult
            {
                Currency = resultComputed.Item2,
                Value = resultComputed.Item1
            };
        }
    }
}
