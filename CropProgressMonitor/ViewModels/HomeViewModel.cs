namespace CropProgressMonitor.ViewModels;

public class HomeViewModel
{
    public int TotalCrops { get; set; }
    public int TotalStages { get; set; }
    public int TotalRegisteredFields { get; set; }
    public List<HomeCropShowcaseItem> Crops { get; set; } = new();
}

public class HomeCropShowcaseItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double BaseTemperature { get; set; }
    public string? Description { get; set; }
    public List<HomeStagePreviewItem> Stages { get; set; } = new();
}

public class HomeStagePreviewItem
{
    public int StageOrder { get; set; }
    public string StageName { get; set; } = string.Empty;
    public double GddThreshold { get; set; }
}
