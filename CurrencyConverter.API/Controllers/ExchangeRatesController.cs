// CurrencyConverter.API/Controllers/ExchangeRatesController.cs
using System.ComponentModel.DataAnnotations;
using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyConverter.API.Controllers;

[ApiController]
[Route("api/v1/rates")]
[Authorize]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateProviderFactory _exchangeRateProviderFactory;
    private readonly ILogger<ExchangeRatesController> _logger;

    public ExchangeRatesController(
        IExchangeRateProviderFactory exchangeRateProviderFactory,
        ILogger<ExchangeRatesController> logger)
    {
        _exchangeRateProviderFactory = exchangeRateProviderFactory ?? throw new ArgumentNullException(nameof(exchangeRateProviderFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get latest exchange rates for a base currency
    /// </summary>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRate>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ExchangeRate>>> GetLatestRates(
        [FromQuery, Required] string baseCurrency,
        [FromQuery] string providerName = "Frankfurter",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching latest rates for base currency: {BaseCurrency}", baseCurrency);

        try
        {
            var provider = _exchangeRateProviderFactory.CreateProvider(providerName);
            var result = await provider.GetLatestRatesAsync(baseCurrency, cancellationToken);

            return Ok(ApiResponse<ExchangeRate>.SuccessResponse(result));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters");
            return BadRequest(ApiResponse<ExchangeRate>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get historical exchange rates for a given period
    /// </summary>
    [HttpGet("historical")]
    [ProducesResponseType(typeof(ApiResponse<HistoricalRatesResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [Authorize(Roles = "Admin,Premium")]
    public async Task<ActionResult<ApiResponse<HistoricalRatesResult>>> GetHistoricalRates(
        [FromQuery, Required] DateTime from,
        [FromQuery, Required] DateTime to,
        [FromQuery, Required] string baseCurrency,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string providerName = "Frankfurter",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching historical rates from {From} to {To} for base currency: {BaseCurrency}, page {Page}, pageSize {PageSize}",
            from, to, baseCurrency, page, pageSize);

        try
        {
            var provider = _exchangeRateProviderFactory.CreateProvider(providerName);
            var result = await provider.GetHistoricalRatesAsync(from, to, baseCurrency, page, pageSize, cancellationToken);

            return Ok(ApiResponse<HistoricalRatesResult>.SuccessResponse(result));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters");
            return BadRequest(ApiResponse<HistoricalRatesResult>.ErrorResponse(ex.Message));
        }
    }
}