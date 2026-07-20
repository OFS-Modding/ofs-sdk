namespace OFS.Sdk;

/// <summary>
/// Durable marker written immediately before the framework enters untrusted mod
/// assembly/entrypoint code. Its survival proves that the process ended before
/// managed control returned to the loader.
/// </summary>
public sealed record ModLoadJournal
{
    public int SchemaVersion { get; init; } = 1;
    public required string SessionId { get; init; }
    public required string ModId { get; init; }
    public required string Version { get; init; }
    public required string Phase { get; init; }
    public required string ManifestPath { get; init; }
    public required string AssemblyPath { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public int ProcessId { get; init; }
}

public sealed record ModQuarantineEntry
{
    public required string ModId { get; init; }
    public required string Version { get; init; }
    public required string Phase { get; init; }
    public required string Reason { get; init; }
    public required string ManifestPath { get; init; }
    public required string AssemblyPath { get; init; }
    public required string SessionId { get; init; }
    public DateTimeOffset DetectedAtUtc { get; init; }
    public int Occurrences { get; init; } = 1;
}

public sealed record ModQuarantineDocument
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<ModQuarantineEntry> Entries { get; init; } = [];
}

/// <summary>Pure validation/recovery rules shared by runtime, CLI and tests.</summary>
public static class ModSafetyDocuments
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumEntries = 1000;

    public static IReadOnlyList<string> Validate(ModLoadJournal? journal)
    {
        var errors = new List<string>();
        if (journal is null)
        {
            errors.Add("Load journal deserialized to null.");
            return errors;
        }
        if (journal.SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported load journal schema {journal.SchemaVersion}.");
        }
        ValidateToken(errors, "sessionId", journal.SessionId, 100);
        if (!IsValidModId(journal.ModId)) errors.Add($"Invalid journal mod id '{journal.ModId}'.");
        if (!ModVersion.TryParse(journal.Version, out _))
        {
            errors.Add($"Invalid journal mod version '{journal.Version}'.");
        }
        ValidateToken(errors, "phase", journal.Phase, 100);
        ValidatePath(errors, "manifestPath", journal.ManifestPath);
        ValidatePath(errors, "assemblyPath", journal.AssemblyPath);
        if (journal.StartedAtUtc == default) errors.Add("Journal startedAtUtc is required.");
        if (journal.ProcessId <= 0) errors.Add("Journal processId must be positive.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(ModQuarantineDocument? document)
    {
        var errors = new List<string>();
        if (document is null)
        {
            errors.Add("Quarantine document deserialized to null.");
            return errors;
        }
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported quarantine schema {document.SchemaVersion}.");
        }
        if (document.Entries is null)
        {
            errors.Add("Quarantine entries must be an array.");
            return errors;
        }
        if (document.Entries.Count > MaximumEntries)
        {
            errors.Add($"Quarantine has more than {MaximumEntries} entries.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.Entries)
        {
            if (entry is null)
            {
                errors.Add("Quarantine contains a null entry.");
                continue;
            }
            if (!IsValidModId(entry.ModId)) errors.Add($"Invalid quarantined mod id '{entry.ModId}'.");
            if (!ids.Add(entry.ModId)) errors.Add($"Duplicate quarantined mod id '{entry.ModId}'.");
            if (!ModVersion.TryParse(entry.Version, out _))
            {
                errors.Add($"Invalid quarantined version '{entry.Version}'.");
            }
            ValidateToken(errors, "phase", entry.Phase, 100);
            ValidateToken(errors, "reason", entry.Reason, 500);
            ValidateToken(errors, "sessionId", entry.SessionId, 100);
            ValidatePath(errors, "manifestPath", entry.ManifestPath);
            ValidatePath(errors, "assemblyPath", entry.AssemblyPath);
            if (entry.DetectedAtUtc == default) errors.Add($"Quarantine '{entry.ModId}' has no detection time.");
            if (entry.Occurrences <= 0) errors.Add($"Quarantine '{entry.ModId}' occurrences must be positive.");
        }
        return errors;
    }

    public static ModQuarantineDocument Recover(
        ModLoadJournal journal,
        ModQuarantineDocument? current,
        DateTimeOffset detectedAtUtc)
    {
        var journalErrors = Validate(journal);
        if (journalErrors.Count != 0)
        {
            throw new InvalidDataException(string.Join(" ", journalErrors));
        }
        var document = current ?? new ModQuarantineDocument();
        var quarantineErrors = Validate(document);
        if (quarantineErrors.Count != 0)
        {
            throw new InvalidDataException(string.Join(" ", quarantineErrors));
        }
        if (detectedAtUtc == default)
        {
            throw new ArgumentException("Detection time is required.", nameof(detectedAtUtc));
        }

        var existing = document.Entries.FirstOrDefault(entry =>
            string.Equals(entry.ModId, journal.ModId, StringComparison.OrdinalIgnoreCase));
        var sameAttempt = existing is not null && string.Equals(
            existing.SessionId,
            journal.SessionId,
            StringComparison.Ordinal);
        var recovered = new ModQuarantineEntry
        {
            ModId = journal.ModId,
            Version = journal.Version,
            Phase = journal.Phase,
            Reason = journal.Phase.StartsWith("callback:", StringComparison.Ordinal)
                ? "Previous game process ended while the framework was executing a guarded runtime callback for this mod."
                : "Previous game process ended while the framework was loading this mod.",
            ManifestPath = journal.ManifestPath,
            AssemblyPath = journal.AssemblyPath,
            SessionId = journal.SessionId,
            DetectedAtUtc = sameAttempt ? existing!.DetectedAtUtc : detectedAtUtc,
            Occurrences = sameAttempt
                ? existing!.Occurrences
                : checked((existing?.Occurrences ?? 0) + 1),
        };
        return new ModQuarantineDocument
        {
            Entries = document.Entries
                .Where(entry => !string.Equals(
                    entry.ModId,
                    journal.ModId,
                    StringComparison.OrdinalIgnoreCase))
                .Append(recovered)
                .OrderBy(entry => entry.ModId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };
    }

    private static bool IsValidModId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 3 and <= 80 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private static void ValidateToken(
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

    private static void ValidatePath(ICollection<string> errors, string property, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024)
        {
            errors.Add($"{property} is required and must contain at most 1024 characters.");
        }
    }
}
