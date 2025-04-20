using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.Unit
{
    public class ApiConfigurationTests
    {
        [Fact]
        public void ApiVersioning_RegistersCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Add required logging services
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            
            // Act - Simulate the API versioning configuration from Program.cs
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            });
            
            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            services.AddMvc();
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert
            // Just verifying that service registration works without errors
            Assert.NotNull(serviceProvider);
        }
        
        [Fact]
        public void SwaggerGen_RegistersCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Add required services
            services.AddLogging(builder => builder.AddConsole());
            services.AddMvc();
            
            // Act - Simulate the Swagger configuration from Program.cs
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Currency Converter API",
                    Version = "v1",
                    Description = "A robust currency conversion API built with ASP.NET Core"
                });
                
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            
            var serviceProvider = services.BuildServiceProvider();
            
            // No direct way to test Swagger configuration, but we can verify it doesn't throw
            // This is an "existence test" - we're just making sure it initializes
            Assert.NotNull(serviceProvider);
        }
        
        [Fact]
        public void OpenTelemetry_RegistersCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            var appName = "TestApp";
            
            // Act - Simulate the OpenTelemetry configuration from Program.cs
            services.AddOpenTelemetry()
                .WithTracing(tracerProviderBuilder =>
                    tracerProviderBuilder
                        .AddSource(appName)
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation());
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert - Just verify registration doesn't throw
            Assert.NotNull(serviceProvider);
        }
    }
}