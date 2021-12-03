﻿using static Rwml.IO.RwmodIO;

namespace Rwml.IO;

// RWMOD file format: https://gist.github.com/Dual-Iron/b28590195548cb382874f0040ec96b78
sealed class RwmodHeader
{
    private const ushort CurrentVersion = 0;

    [Flags]
    public enum FileFlags : byte
    {
        IsModEntry
    }

    public RwmodHeader(FileFlags flags, SemVer version, string modName, string modOwner, string homepage)
    {
        Flags = flags;
        Version = version;
        Name = modName;
        Owner = modOwner;
        Homepage = homepage;
    }

    public readonly FileFlags Flags;
    public readonly SemVer Version;
    public readonly string Name;
    public readonly string Owner;
    public readonly string Homepage;

    public void Write(Stream s)
    {
        s.Write(new byte[] { 0x5, 0x57, 0x4d, 0x4f, 0x44 }, 0, 5);
        WriteUInt16(s, CurrentVersion);

        s.WriteByte((byte)Flags);
        WriteStringFull(s, Version.ToString());
        WriteStringFull(s, Name);
        WriteStringFull(s, Owner);
        WriteStringFull(s, Homepage);
    }

    public static Result<RwmodHeader, string> Read(Stream s)
    {
        byte[] b = new byte[5];

        s.Read(b, 0, 5);

        if (b[0] != 0x5 || b[1] != 0x57 || b[2] != 0x4d || b[3] != 0x4f || b[4] != 0x44) {
            return "not a rwmod file";
        }

        int version = ReadUInt16(ref b, s);
        if (version == CurrentVersion) {
            return ReadV0(b, s);
        }

        return "can't read future version; upgrade Realm";
    }

    static Result<RwmodHeader, string> ReadV0(byte[] b, Stream s)
    {
        var flags = s.ReadByte();
        if (flags > 1) {
            return "invalid flags";
        }

        try {
            var semVer = ReadStringFull(ref b, s);

            if (SemVer.Parse(semVer) is not SemVer version) {
                return "invalid semantic version";
            }

            var modName = ReadStringFull(ref b, s);
            var modOwner = ReadStringFull(ref b, s);
            var homepage = ReadStringFull(ref b, s);

            return new RwmodHeader((FileFlags)flags, version, modName, modOwner, homepage);
        }
        catch {
            return "corrupt file";
        }
    }
}