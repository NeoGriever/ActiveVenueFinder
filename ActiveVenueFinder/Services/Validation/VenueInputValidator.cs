using System.Collections.Generic;
using System.Linq;

namespace ActiveVenueFinder.Services.Validation;

public sealed class ValidationResult
{
    public bool Ok => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}

// Validates user-entered venue data so AddEditVenueWindow can show inline errors
// instead of silently swallowing duplicates or empty fields.
public static class VenueInputValidator
{
    public sealed record Input(
        string Name,
        string World,
        string District,
        int Ward,
        int Plot,
        int? Apartment,
        bool IsCustomMode,
        int EditingCustomId);

    public static ValidationResult Validate(Input input, Config config)
    {
        var r = new ValidationResult();

        if (string.IsNullOrWhiteSpace(input.Name))
            r.Errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(input.World))
            r.Errors.Add("World is required.");
        if (string.IsNullOrWhiteSpace(input.District))
            r.Errors.Add("District is required.");

        if (input.Ward < 1 || input.Ward > 30)
            r.Errors.Add("Ward must be between 1 and 30.");

        if (input.Apartment is { } a)
        {
            if (a < 1 || a > 512)
                r.Errors.Add("Apartment must be between 1 and 512.");
        }
        else
        {
            if (input.Plot < 1 || input.Plot > 60)
                r.Errors.Add("Plot must be between 1 and 60.");
        }

        if (!Services.VenueResolver.WorldToDataCenter.ContainsKey(input.World)
            && !string.IsNullOrWhiteSpace(input.World))
        {
            r.Warnings.Add($"World '{input.World}' is not a known FFXIV world.");
        }

        if (input.IsCustomMode)
        {
            var duplicate = config.CustomVenues.Any(cv =>
                cv.Id != input.EditingCustomId
                && string.Equals(cv.World, input.World, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(cv.District, input.District, System.StringComparison.OrdinalIgnoreCase)
                && cv.Ward == input.Ward
                && cv.Plot == input.Plot
                && cv.Apartment == input.Apartment
                && cv.Subdivision == false);
            if (duplicate)
                r.Errors.Add("A custom venue already exists at this location.");
        }

        return r;
    }
}
