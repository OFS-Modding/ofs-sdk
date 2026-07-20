using System.Globalization;

namespace OFS.Sdk;

/// <summary>A stable three-component semantic version used by manifests and catalogs.</summary>
public readonly record struct ModVersion(int Major, int Minor, int Patch) : IComparable<ModVersion>
{
    public static bool TryParse(string? value, out ModVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 3 || parts.Any(part =>
                part.Length == 0 ||
                (part.Length > 1 && part[0] == '0') ||
                !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
                parsed < 0))
        {
            return false;
        }

        version = new ModVersion(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(parts[2], CultureInfo.InvariantCulture));
        return true;
    }

    public int CompareTo(ModVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/// <summary>
/// An AND range such as <c>&gt;=1.0.0,&lt;2.0.0</c>. A bare version means exact;
/// <c>*</c> accepts every stable version.
/// </summary>
public sealed class ModVersionRange
{
    private readonly IReadOnlyList<Comparator> _comparators;

    private ModVersionRange(string source, IReadOnlyList<Comparator> comparators)
    {
        Source = source;
        _comparators = comparators;
    }

    public string Source { get; }

    public static bool TryParse(string? value, out ModVersionRange? range)
    {
        range = null;
        var source = string.IsNullOrWhiteSpace(value) ? "*" : value.Trim();
        if (source == "*")
        {
            range = new ModVersionRange(source, []);
            return true;
        }

        var comparators = new List<Comparator>();
        foreach (var rawPart in source.Split(',', StringSplitOptions.TrimEntries))
        {
            if (rawPart.Length == 0)
            {
                return false;
            }

            var operation = Comparison.Equal;
            var versionText = rawPart;
            foreach (var candidate in new[] { ">=", "<=", ">", "<", "=" })
            {
                if (!rawPart.StartsWith(candidate, StringComparison.Ordinal)) continue;
                operation = candidate switch
                {
                    ">=" => Comparison.GreaterThanOrEqual,
                    "<=" => Comparison.LessThanOrEqual,
                    ">" => Comparison.GreaterThan,
                    "<" => Comparison.LessThan,
                    _ => Comparison.Equal,
                };
                versionText = rawPart[candidate.Length..].Trim();
                break;
            }

            if (!ModVersion.TryParse(versionText, out var version))
            {
                return false;
            }
            comparators.Add(new Comparator(operation, version));
        }

        range = new ModVersionRange(source, comparators);
        return true;
    }

    public bool Contains(ModVersion version) => _comparators.All(comparator => comparator.Matches(version));

    public override string ToString() => Source;

    private enum Comparison
    {
        Equal,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    private readonly record struct Comparator(Comparison Operation, ModVersion Version)
    {
        public bool Matches(ModVersion candidate)
        {
            var comparison = candidate.CompareTo(Version);
            return Operation switch
            {
                Comparison.Equal => comparison == 0,
                Comparison.GreaterThan => comparison > 0,
                Comparison.GreaterThanOrEqual => comparison >= 0,
                Comparison.LessThan => comparison < 0,
                Comparison.LessThanOrEqual => comparison <= 0,
                _ => false,
            };
        }
    }
}
