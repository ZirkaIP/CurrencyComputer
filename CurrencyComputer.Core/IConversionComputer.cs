namespace CurrencyComputer.Core
{
    public interface IConversionComputer
    {
        ComputeResult Compute(string input);
    }
}
