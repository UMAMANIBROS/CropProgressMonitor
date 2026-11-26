using CropProgressMonitor.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CropProgressMonitor.ViewModels;

public class AdvisoryViewModel
{
    public int FieldCropId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string CropName { get; set; } = string.Empty;
    public double AccumulatedGdd { get; set; }
    public string Language { get; set; } = "en";
    public StageAdvisoryResult Advisory { get; set; } = new();
    public List<SelectListItem> FieldCrops { get; set; } = new();
}
