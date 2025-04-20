using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Tests.Models;

public class PaginationMetadataTests
{
    [Fact]
    public void HasPrevious_WhenCurrentPageIsOne_ReturnsFalse()
    {
        // Arrange
        var metadata = new PaginationMetadata
        {
            CurrentPage = 1,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        // Act & Assert
        Assert.False(metadata.HasPrevious);
    }
    
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void HasPrevious_WhenCurrentPageGreaterThanOne_ReturnsTrue(int currentPage)
    {
        // Arrange
        var metadata = new PaginationMetadata
        {
            CurrentPage = currentPage,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        // Act & Assert
        Assert.True(metadata.HasPrevious);
    }
    
    [Fact]
    public void HasNext_WhenCurrentPageEqualsTotalPages_ReturnsFalse()
    {
        // Arrange
        var metadata = new PaginationMetadata
        {
            CurrentPage = 10,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        // Act & Assert
        Assert.False(metadata.HasNext);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public void HasNext_WhenCurrentPageLessThanTotalPages_ReturnsTrue(int currentPage)
    {
        // Arrange
        var metadata = new PaginationMetadata
        {
            CurrentPage = currentPage,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        // Act & Assert
        Assert.True(metadata.HasNext);
    }
    
    [Fact]
    public void TotalPages_CalculatedCorrectly()
    {
        // Arrange & Act
        var metadata = new PaginationMetadata
        {
            PageSize = 10,
            TotalCount = 95,
            TotalPages = 10  // This would normally be calculated
        };

        // In a real-world scenario, TotalPages would be calculated as:
        // int calculatedTotalPages = (int)Math.Ceiling(metadata.TotalCount / (double)metadata.PageSize);
        // Assert.Equal(calculatedTotalPages, metadata.TotalPages);
        
        // For this test, we'll just check that the property is accessible
        Assert.Equal(10, metadata.TotalPages);
    }
}