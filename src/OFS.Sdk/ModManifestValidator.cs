namespace OFS.Sdk;

/// <summary>Validates the portable, runtime-independent portion of an OFS mod manifest.</summary>
public static class ModManifestValidator
{
    public const int CurrentSchemaVersion = 1;
    public static Version CurrentSdkVersion { get; } = new(0, 1, 0);

    public static IReadOnlyList<string> Validate(ModManifest? manifest)
    {
        var errors = new List<string>();
        if (manifest is null)
        {
            errors.Add("Manifest deserialized to null.");
            return errors;
        }

        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add(
                $"Unsupported schemaVersion {manifest.SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id) ||
            manifest.Id.Length is < 3 or > 80 ||
            manifest.Id.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            errors.Add("id must be 3-80 ASCII letters, digits, dots, dashes or underscores.");
        }

        ValidateRequiredLength(errors, "name", manifest.Name, 100);
        ValidateRequiredLength(errors, "assembly", manifest.Assembly, 260);
        ValidateRequiredLength(errors, "entryPoint", manifest.EntryPoint, 300);
        ValidateOptionalLength(errors, "description", manifest.Description, 1000);
        ValidateOptionalLength(errors, "author", manifest.Author, 100);

        if (!ModVersion.TryParse(manifest.Version, out _))
        {
            errors.Add($"version '{manifest.Version}' must be a stable three-component semantic version.");
        }

        if (!Version.TryParse(manifest.SdkVersion, out var sdkVersion))
        {
            errors.Add($"sdkVersion '{manifest.SdkVersion}' is not a valid version.");
        }
        else if (sdkVersion.Major != CurrentSdkVersion.Major || sdkVersion.Minor > CurrentSdkVersion.Minor)
        {
            errors.Add(
                $"sdkVersion {sdkVersion} is incompatible with runtime {CurrentSdkVersion}.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Assembly) &&
            !string.Equals(Path.GetExtension(manifest.Assembly), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("assembly must point to a .dll file.");
        }

        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in manifest.Dependencies ?? [])
        {
            if (!IsValidId(dependency.Id))
            {
                errors.Add($"dependency id '{dependency.Id}' is invalid.");
            }
            else if (!dependencies.Add(dependency.Id))
            {
                errors.Add($"dependency '{dependency.Id}' is declared more than once.");
            }
            else if (string.Equals(dependency.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("a mod cannot depend on itself.");
            }

            if (!ModVersionRange.TryParse(dependency.Version, out _))
            {
                errors.Add($"dependency '{dependency.Id}' has invalid version range '{dependency.Version}'.");
            }
        }

        foreach (var capability in manifest.Capabilities ?? [])
        {
            if (!IsValidId(capability))
            {
                errors.Add($"capability '{capability}' is invalid.");
            }
        }

        if (manifest.Multiplayer is not ("unknown" or "client" or "server" or "required" or "incompatible"))
        {
            errors.Add("multiplayer must be unknown, client, server, required or incompatible.");
        }

        return errors;
    }

    internal static bool IsValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 3 and <= 80 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private static void ValidateRequiredLength(
        ICollection<string> errors,
        string property,
        string? value,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
        {
            errors.Add($"{property} is required and must contain at most {maximum} characters.");
        }
    }

    private static void ValidateOptionalLength(
        ICollection<string> errors,
        string property,
        string? value,
        int maximum)
    {
        if (value?.Length > maximum)
        {
            errors.Add($"{property} must contain at most {maximum} characters.");
        }
    }
}
