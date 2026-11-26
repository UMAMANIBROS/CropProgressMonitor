namespace CropProgressMonitor.Models;

/// <summary>
/// Persisted daily GDD and growth stage estimate for a planted crop in a field.
/// </summary>
public class GrowthEstimate
{
    public int Id { get; set; }

    public int FieldCropId { get; set; }

    public DateTime EstimateDate { get; set; }

    public double AccumulatedGdd { get; set; }

    public int CurrentStageId { get; set; }

    public int? NextStageId { get; set; }

    public DateTime? NextStageDate { get; set; }

    public FieldCrop? FieldCrop { get; set; }

    public CropStage? CurrentStage { get; set; }

    public CropStage? NextStage { get; set; }
}
