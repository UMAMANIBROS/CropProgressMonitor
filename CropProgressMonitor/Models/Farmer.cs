using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CropProgressMonitor.Models;

/// <summary>
/// Represents a registered farmer (application user) stored in the Farmers table.
/// </summary>
public class Farmer : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Preferred advisory language code: "en" (English), "hi" (Hindi), or "pa" (Punjabi).
    /// </summary>
    [Required]
    [StringLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    public ICollection<Field> Fields { get; set; } = new List<Field>();
}
