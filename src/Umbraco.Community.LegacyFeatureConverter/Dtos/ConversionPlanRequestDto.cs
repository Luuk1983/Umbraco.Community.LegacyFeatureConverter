using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Dtos;

/// <summary>
/// Request model for computing a conversion plan.
/// </summary>
public class ConversionPlanRequestDto
{
    /// <summary>
    /// Gets or sets the name of the converter to plan for.
    /// </summary>
    public string ConverterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the approach to use when discovering affected document types and content.
    /// </summary>
    public ConversionApproach Approach { get; set; } = ConversionApproach.Fast;
}
