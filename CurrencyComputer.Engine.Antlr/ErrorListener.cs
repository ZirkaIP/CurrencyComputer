using Antlr4.Runtime;

using CurrencyComputer.Core;

using System.IO;

namespace CurrencyComputer.Engine.Antlr
{
    internal sealed class ErrorListener : IAntlrErrorListener<int>
    {
        public void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            int offendingSymbol,
            int line,
            int charPositionInLine,
            string msg, RecognitionException e)
        {
            throw new SyntaxException($"{msg}. Position: {charPositionInLine}.");
        }
    }
}
