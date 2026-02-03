using System.ComponentModel.DataAnnotations;

namespace Application.Settings;

public class DatabaseSettings
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage = "Database connection string 'Default' is required")]
    public string Default { get; init; } = string.Empty;

    public string? Redis { get; init; }
}
