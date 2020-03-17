using System;

namespace CurrencyComputer.Core
{
    public sealed class SyntaxException : Exception
    {
        public SyntaxException(string message) : base(message) { }
    }
}
