namespace CurrencyConverter.Core.Models;

public class HistoricalRatesResult
{
    public required string Base { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public required List<ExchangeRate> Rates { get; set; }
    public required PaginationMetadata Pagination { get; set; }
}