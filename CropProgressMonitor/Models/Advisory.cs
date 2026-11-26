using System.ComponentModel.DataAnnotations;

namespace CropProgressMonitor.Models;

/// <summary>
/// Stage-specific agronomic guidance covering water, nutrients, and common risks in English, Hindi, and Punjabi.
/// </summary>
public class Advisory
{
    public int Id { get; set; }

    public int CropStageId { get; set; }

    [Required]
    [StringLength(1000)]
    public string WaterAdvice { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string NutrientAdvice { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string RiskAdvice { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? WaterAdviceHi { get; set; }

    [StringLength(1000)]
    public string? NutrientAdviceHi { get; set; }

    [StringLength(1000)]
    public string? RiskAdviceHi { get; set; }

    [StringLength(1000)]
    public string? WaterAdvicePa { get; set; }

    [StringLength(1000)]
    public string? NutrientAdvicePa { get; set; }

    [StringLength(1000)]
    public string? RiskAdvicePa { get; set; }

    public CropStage? CropStage { get; set; }
}
