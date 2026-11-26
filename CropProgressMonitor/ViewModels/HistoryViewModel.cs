using Microsoft.AspNetCore.Mvc.Rendering;

namespace CropProgressMonitor.ViewModels;

public class HistoryViewModel
{
    public int SelectedFieldCropId { get; set; }
    public int? CompareFieldCropId { get; set; }
    public string SelectedLabel { get; set; } = string.Empty;
    public string? CompareLabel { get; set; }
    public List<SelectListItem> FieldCrops { get; set; } = new();
    public List<HistoryRowViewModel> Estimates { get; set; } = new();
    public List<ChartPointViewModel> PrimarySeries { get; set; } = new();
    public List<ChartPointViewModel> CompareSeries { get; set; } = new();
}

public class HistoryRowViewModel
{
    public DateTime EstimateDate { get; set; }
    public double AccumulatedGdd { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public string? NextStageName { get; set; }
    public DateTime? NextStageDate { get; set; }
}

public class ChartPointViewModel
{
    public string DateLabel { get; set; } = string.Empty;
    public int DaysAfterSowing { get; set; }
    public double AccumulatedGdd { get; set; }
}
