namespace CurrencyConverter.Core.Models
{
    public class ConversionResult
    {
        public required string FromCurrency { get; set; }
        public required string ToCurrency { get; set; }
        public decimal Amount { get; set; }
        public decimal ConvertedAmount { get; set; }
        public decimal Rate { get; set; }
        public DateTime Date { get; set; }
    }
}