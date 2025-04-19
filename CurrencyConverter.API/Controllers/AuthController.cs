using System.ComponentModel.DataAnnotations;
using CurrencyConverter.Core.Models;
using CurrencyConverter.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyConverter.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IJwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public class LoginRequest
    {
        [Required]
        public required string Username { get; set; }

        [Required]
        public required string Password { get; set; }
    }

    public class LoginResponse
    {
        public required string Token { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public required string[] Roles { get; set; }
    }

    /// <summary>
    /// Login to get JWT token
    /// </summary>
    /// <remarks>
    /// Sample login credentials:
    /// - Regular user: username=user, password=password
    /// - Admin user: username=admin, password=admin
    /// - Premium user: username=premium, password=premium
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        // For demo purposes, we'll use hardcoded credentials
        // In a real app, this would validate against a database
        if (request.Username == "admin" && request.Password == "admin")
        {
            var roles = new[] { "Admin" };
            var token = _jwtTokenService.GenerateToken(request.Username, roles);

            return Ok(ApiResponse<LoginResponse>.SuccessResponse(new LoginResponse
            {
                Token = token,
                ExpiresIn = 60 * 60, // 1 hour
                Roles = roles
            }));
        }
        else if (request.Username == "premium" && request.Password == "premium")
        {
            var roles = new[] { "Premium" };
            var token = _jwtTokenService.GenerateToken(request.Username, roles);

            return Ok(ApiResponse<LoginResponse>.SuccessResponse(new LoginResponse
            {
                Token = token,
                ExpiresIn = 60 * 60, // 1 hour
                Roles = roles
            }));
        }
        else if (request.Username == "user" && request.Password == "password")
        {
            var roles = new[] { "User" };
            var token = _jwtTokenService.GenerateToken(request.Username, roles);

            return Ok(ApiResponse<LoginResponse>.SuccessResponse(new LoginResponse
            {
                Token = token,
                ExpiresIn = 60 * 60, // 1 hour
                Roles = roles
            }));
        }

        return Unauthorized(ApiResponse<LoginResponse>.ErrorResponse("Invalid username or password"));
    }
}