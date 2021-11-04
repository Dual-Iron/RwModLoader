using System.Text.RegularExpressions;

namespace Rwml;

// https://semver.org/
readonly struct SemVer : IComparable<SemVer>
{
    private static Regex? _semVerRegex;
    private static Regex SemVerRegex => _semVerRegex ??= new Regex(@"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$");

    public readonly int Major;
    public readonly int Minor;
    public readonly int Patch;
    public readonly string PreRelease;
    public readonly string BuildMetadata;

    public SemVer(Version version) : this(version.Major, version.Minor, version.Build) { }

    public SemVer(int major, int minor, int patch, string preRelease = "", string buildMetadata = "")
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        BuildMetadata = buildMetadata;
    }

    public int CompareTo(SemVer other)
    {
        // Precedence MUST be calculated by separating the version into major, minor, patch and pre-release identifiers in that order
        // Major, minor, and patch versions are always compared numerically.
        if (Major != other.Major)
            return Major.CompareTo(other.Major);
        if (Minor != other.Minor)
            return Minor.CompareTo(other.Minor);
        if (Patch != other.Patch)
            return Patch.CompareTo(other.Patch);

        // This is a quick check for a common case of equality
        if (PreRelease == other.PreRelease)
            return 0;

        // When major, minor, and patch are equal, a pre-release version has lower precedence than a normal version.
        if (PreRelease.Length == 0 && other.PreRelease.Length > 0)
            return 1;
        if (PreRelease.Length > 0 && other.PreRelease.Length == 0)
            return -1;

        // Precedence for two pre-release versions with the same major, minor, and patch version MUST be determined by
        // comparing each dot separated identifier from left to right until a difference is found.
        string[] ids1 = PreRelease.Split('.');
        string[] ids2 = other.PreRelease.Split('.');

        int min = Math.Min(ids1.Length, ids2.Length);

        for (int i = 0; i < min; i++) {
            string id1 = ids1[i], id2 = ids2[i];

            int c = ComparePreReleaseIdentifiers(id1, id2);
            if (c != 0)
                return c;
        }

        // A larger set of pre-release fields has a higher precedence than a smaller set, if all of the preceding identifiers are equal.
        return ids1.Length - ids2.Length;
    }

    public override bool Equals(object? obj)
    {
        return obj is SemVer semver && CompareTo(semver) == 0;
    }

    public override string ToString()
    {
        string ret = $"{Major}.{Minor}.{Patch}";

        if (PreRelease.Length > 0) ret += $"-{PreRelease}";
        if (BuildMetadata.Length > 0) ret += $"+{BuildMetadata}";

        return ret;
    }

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0070 // Don't use 'System.HashCode'. This code is shared with Realm.
    public override int GetHashCode()
    {
        int hashCode = -453701506;
        hashCode = hashCode * -1521134295 + Major.GetHashCode();
        hashCode = hashCode * -1521134295 + Minor.GetHashCode();
        hashCode = hashCode * -1521134295 + Patch.GetHashCode();
        hashCode = hashCode * -1521134295 + PreRelease.GetHashCode();
        return hashCode;
    }

    public static bool operator ==(SemVer left, SemVer right) => left.Equals(right);
    public static bool operator !=(SemVer left, SemVer right) => !(left == right);
    public static bool operator <(SemVer left, SemVer right) => left.CompareTo(right) < 0;
    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;
    public static bool operator >(SemVer left, SemVer right) => left.CompareTo(right) > 0;
    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;

    public static SemVer? Parse(string str)
    {
        Match match = SemVerRegex.Match(str);

        if (!match.Success) return null;

        // If the match succeeded, then this will, too. There's no point in checking.
        _ = int.TryParse(match.Groups["major"].Value, out int major);
        _ = int.TryParse(match.Groups["minor"].Value, out int minor);
        _ = int.TryParse(match.Groups["patch"].Value, out int patch);

        return new(major, minor, patch, match.Groups["prerelease"].Value, match.Groups["buildmetadata"].Value);
    }

    private static int ComparePreReleaseIdentifiers(string id1, string id2)
    {
        if (int.TryParse(id1, out int int1)) {
            if (int.TryParse(id2, out int int2)) {
                // Identifiers consisting of only digits are compared numerically.
                return int1 - int2;
            }
            // Numeric identifiers always have lower precedence than non-numeric identifiers.
            return -1;
        }

        if (int.TryParse(id2, out _)) {
            // Numeric identifiers always have lower precedence than non-numeric identifiers.
            return 1;
        }

        return StringComparer.Ordinal.Compare(id1, id2);
    }
}
