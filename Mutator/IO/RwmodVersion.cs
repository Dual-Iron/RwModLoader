using System.Text.RegularExpressions;

namespace Mutator.IO
{
    public struct RwmodVersion
    {
        public RwmodVersion(Version version) : this((byte)version.Major, (byte)version.Minor, (byte)version.Build)
        {
        }

        public RwmodVersion(byte major, byte minor, byte patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public byte Major { get; }
        public byte Minor { get; }
        public byte Patch { get; }

        public Version ToVersion() => new(Major, Minor, Patch);

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
    }
}
