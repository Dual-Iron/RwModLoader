using System.Text.RegularExpressions;

namespace Mutator.IO;

record struct RwmodVersion : IComparable<RwmodVersion>
{
    public RwmodVersion(Version version) : this((byte)version.Major, (byte)version.Minor, (byte)version.Build)
    {
    }

    public RwmodVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public static bool TryParse(string versionString, out RwmodVersion version)
    {
        version = default;

        Match match = Regex.Match(versionString, @"[vV]?(\d)(?:.(\d)(?:.(\d))?)?");

        if (!match.Success) return false;

        version = new(
            byte.Parse(match.Groups[1].Value),
            match.Groups[2].Success ? byte.Parse(match.Groups[2].Value) : default,
            match.Groups[3].Success ? byte.Parse(match.Groups[3].Value) : default
            );

        return true;
    }

    public int CompareTo(RwmodVersion other)
    {
        if (Major != other.Major)
            return Major.CompareTo(other.Major);
        if (Minor != other.Minor)
            return Minor.CompareTo(other.Minor);
        if (Patch != other.Patch)
            return Patch.CompareTo(other.Patch);
        return 0;
    }

    public static bool operator <(RwmodVersion left, RwmodVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(RwmodVersion left, RwmodVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(RwmodVersion left, RwmodVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(RwmodVersion left, RwmodVersion right)
    {
        return left.CompareTo(right) >= 0;
    }
}
