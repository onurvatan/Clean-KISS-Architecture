using System.ComponentModel.DataAnnotations;

namespace Application.Settings;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "JWT Secret is required")]
    [MinLength(32, ErrorMessage = "JWT Secret must be at least 32 characters")]
    public string Secret { get; init; } = string.Empty;

    [Required(ErrorMessage = "JWT Issuer is required")]
    public string Issuer { get; init; } = string.Empty;

    [Required(ErrorMessage = "JWT Audience is required")]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "JWT ExpirationMinutes must be between 1 and 1440")]
    public int ExpirationMinutes { get; init; } = 60;
}
