using System.ComponentModel.DataAnnotations;
using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyConverter.API.Controllers;

[ApiController]
[Route("api/v1/convert")]
[Authorize]
public class ConversionController : ControllerBase
{
    private readonly IExchangeRateProviderFactory _exchangeRateProviderFactory;
    private readonly ILogger<ConversionController> _logger;

    public ConversionController(
        IExchangeRateProviderFactory exchangeRateProviderFactory,
        ILogger<ConversionController> logger)
    {
        _exchangeRateProviderFactory = exchangeRateProviderFactory ?? throw new ArgumentNullException(nameof(exchangeRateProviderFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Convert an amount from one currency to another
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ConversionResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ConversionResult>>> ConvertCurrency(
        [FromQuery, Required] string from,
        [FromQuery, Required] string to,
        [FromQuery, Required] decimal amount,
        [FromQuery] string providerName = "Frankfurter",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Converting {Amount} from {From} to {To}", amount, from, to);

        try
        {
            var provider = _exchangeRateProviderFactory.CreateProvider(providerName);
            var result = await provider.ConvertCurrencyAsync(from, to, amount, cancellationToken);

            return Ok(ApiResponse<ConversionResult>.SuccessResponse(result));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters");
            return BadRequest(ApiResponse<ConversionResult>.ErrorResponse(ex.Message));
        }
    }
}