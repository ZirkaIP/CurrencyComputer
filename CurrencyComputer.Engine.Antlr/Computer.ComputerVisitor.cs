using CurrencyComputer.Core;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CurrencyComputer.Engine.Antlr
{
    public sealed partial class Computer : IConversionComputer
    {
        private sealed class ComputerVisitor : CurrencyComputerBaseVisitor<object>
        {
            private static readonly IReadOnlyDictionary<string, Func<decimal, decimal, decimal>> Operations = new ReadOnlyDictionary<string, Func<decimal, decimal, decimal>>(new Dictionary<string, Func<decimal, decimal, decimal>>()
            {
                { "++", (l, r) => l + r },
                { "--", (l, r) => -(l + r) },
                { "-+", (l, r) => (r - l) },
                { "+-", (l, r) => (l - r) },
            });

            private readonly IDictionary<string, Dictionary<string, decimal>> _conversionsCost;
            private readonly IDictionary<string, string> _conversionToCurrencyConventions;

            private readonly ILogger _logger;

            private string _targetCurrency;
            private int _operationsLogger;

            public ComputerVisitor(
                IDictionary<string, Dictionary<string, decimal>> conversionsCost,
                IDictionary<string, string> conversionToCurrencyConventions,
                ILogger logger)
            {
                _conversionsCost = conversionsCost;
                _conversionToCurrencyConventions = conversionToCurrencyConventions;
                _logger = logger;
            }

            public override object VisitConversion(CurrencyComputerParser.ConversionContext context)
            {
                var conversion = context.GetText();
                return conversion;
            }

            public override object VisitAmountSignedConvertible(CurrencyComputerParser.AmountSignedConvertibleContext context)
            {
                var amountSigned = (AmountSigned)VisitAmountSigned(context.amountSigned());
                // Контроль типов: нельзя конвертировать валюту в саму себя
                if (amountSigned.Amount.Currency == _targetCurrency)
                {
                    throw new SyntaxException($"Can't convert {context.GetText()}, because {amountSigned.Amount.Currency} is destination currency.");
                }

                var conversionStr = (string) VisitConversion(context.conversion());
                var convertTo = _conversionToCurrencyConventions[conversionStr];

                var converted = ConvertTo(amountSigned, convertTo);
                if (converted != amountSigned)
                {
                    _logger?.LogDebug("{OperationNumber}:Converted from {Source} to {Dest}.", _operationsLogger++, amountSigned, converted);
                }

                return converted;
            }

            public override object VisitNumber(CurrencyComputerParser.NumberContext context)
            {
                var str = context.GetText();
                return decimal.Parse(str);
            }

            public override object VisitAmountSigned(CurrencyComputerParser.AmountSignedContext context)
            {
                var operatorCtx = context.operatorAndSpaces();
                var sign = operatorCtx is null
                    ? "+"
                    : (string)VisitOperatorAndSpaces(context.operatorAndSpaces());

                return new AmountSigned
                {
                    Sign = sign,
                    Amount = (Amount) VisitAmount(context.amount())
                };
            }

            public override object VisitOperatorAndSpaces(CurrencyComputerParser.OperatorAndSpacesContext context)
            {
                return VisitOperator(context.@operator());
            }

            public override object VisitAmount(CurrencyComputerParser.AmountContext context)
            {
                var currency = context.currencyRight()?.GetText() ?? context.currencyLeft()?.GetText();
                var value = (decimal)VisitNumber(context.number());

                return new Amount
                {
                    Currency = currency,
                    Value = value
                };
            }

            public override object VisitOperator(CurrencyComputerParser.OperatorContext context)
            {
                var value = context.GetText();
                return value;
            }

            public override object VisitAmountComposite(CurrencyComputerParser.AmountCompositeContext context)
            {
                var amountSignedContext = context.amountSigned();
                var result = (AmountSigned)(amountSignedContext is null
                    ? VisitAmountSignedConvertible(context.amountSignedConvertible())
                    : VisitAmountSigned(amountSignedContext));

                return result;
            }

            public override object VisitExpression(CurrencyComputerParser.ExpressionContext context)
            {
                var amounts = context.amountComposite();
                var left = (AmountSigned)VisitAmountComposite(amounts[0]);
                var leftConverted = ConvertTo(left, _targetCurrency);
                if (leftConverted != left)
                {
                    _logger?.LogDebug("{OperationNumber}:Converted from {Source} to {Dest}.", _operationsLogger++, left, leftConverted);
                }

                var right = amounts.Length == 2
                    ? (AmountSigned)VisitAmountComposite(amounts[1])
                    : (AmountSigned) VisitExpression(context.expression());
                var rightConverted = ConvertTo(right, _targetCurrency);
                if (rightConverted != right)
                {
                    _logger?.LogDebug("{OperationNumber}:Converted from {Source} to {Dest}.", _operationsLogger++, right, rightConverted);
                }


                var operationKey = $"{leftConverted.Sign}{rightConverted.Sign}";
                var resultComputed = Operations[operationKey](leftConverted.Amount.Value, rightConverted.Amount.Value);

                var result = new AmountSigned
                {
                    Sign = resultComputed < 0
                        ? "-"
                        : "+",
                    Amount = new Amount
                    {
                        Currency = _targetCurrency,
                        Value = Math.Abs(resultComputed)
                    }
                };

                _logger?.LogDebug("{OperationNumber}:Result {Result} from left {Left} and right {Right} tokens.", _operationsLogger++, result, leftConverted, rightConverted);

                return result;
            }

            public override object VisitInput(CurrencyComputerParser.InputContext context)
            {
                _targetCurrency = null;
                _operationsLogger = 0;

                var conversion = (string)VisitConversion(context.conversion());
                _targetCurrency = _conversionToCurrencyConventions[conversion];

                var resultAmount = (AmountSigned)VisitExpression(context.expression());
                var resultAmountConverted = ConvertTo(resultAmount, _targetCurrency);

                var val = resultAmountConverted.Sign == "-"
                    ? -resultAmountConverted.Amount.Value
                    : resultAmountConverted.Amount.Value;
                return new Tuple<decimal, string>(val, resultAmountConverted.Amount.Currency);
            }

            private AmountSigned ConvertTo(AmountSigned amountSigned, string convertTo)
            {
                // Контроль типов: нельзя конвертировать валюту в саму себя
                if (amountSigned.Amount.Currency == convertTo)
                {
                    return amountSigned;
                }

                var cost = _conversionsCost[amountSigned.Amount.Currency][convertTo];
                return new AmountSigned
                {
                    Sign = amountSigned.Sign,
                    Amount = new Amount
                    {
                        Currency = convertTo,
                        Value = amountSigned.Amount.Value * cost
                    }
                };
            }

            private sealed class Amount
            {
                public string Currency { get; set; }
                public decimal Value { get; set; }

                public override string ToString() =>
                    $"{nameof(Value)}:{Value.ToString(CultureInfo.InvariantCulture)}, {nameof(Currency)}:{Currency}";

                public override bool Equals(object obj)
                    => Equals(obj as Amount, this);

                private  static bool Equals(Amount left, Amount right)
                {
                    if (left is null && right is null) return true;


                    if (left?.GetType() != right?.GetType())
                    {
                        return false;
                    }

                    if (left.GetType() != typeof(Amount) || right.GetType() != typeof(Amount))
                    {
                        return false;
                    }

                    return left.ToString().Equals(right.ToString());
                }

                public static bool operator ==(Amount left, Amount right)
                    => Equals(left, right);

                public static bool operator !=(Amount left, Amount right)
                    => !Equals(left, right);
            }

            private sealed class AmountSigned
            {
                public Amount Amount { get; set; }
                public string Sign { get; set; }

                public override string ToString()
                    => $"{nameof(Sign)}:{Sign}, {Amount}";

                public override bool Equals(object obj)
                    => Equals(obj as AmountSigned, this);

                private static bool Equals(AmountSigned left, AmountSigned right)
                {
                    if (left is null && right is null) return true;


                    if (left?.GetType() != right?.GetType())
                    {
                        return false;
                    }

                    if (left.GetType() != typeof(AmountSigned) || right.GetType() != typeof(AmountSigned))
                    {
                        return false;
                    }

                    return left.ToString().Equals(right.ToString());
                }

                public static bool operator ==(AmountSigned left, AmountSigned right)
                    => Equals(left, right);

                public static bool operator !=(AmountSigned left, AmountSigned right)
                    => !Equals(left, right);
            }
        }
    }
}
