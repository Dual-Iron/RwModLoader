namespace Realm.ModLoading;

public struct RwmodVersion
{
    public RwmodVersion(byte major, byte minor, byte patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public RwmodVersion(Version version) : this((byte)version.Major, (byte)version.Minor, (byte)version.Build)
    { }

    public byte Major { get; }
    public byte Minor { get; }
    public byte Patch { get; }
}
