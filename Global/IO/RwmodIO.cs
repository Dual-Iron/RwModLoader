using System.Text;

namespace Rwml.IO;

static class RwmodIO
{
    public static long CopyStream(Stream input, Stream output, long bytes)
    {
        byte[] buffer = new byte[Math.Min(bytes, 32768)];
        int read;
        while (bytes > 0 && (read = input.Read(buffer, 0, (int)Math.Min(bytes, buffer.Length))) > 0) {
            output.Write(buffer, 0, read);
            bytes -= read;
        }
        return bytes;
    }

    static void Read(ref byte[] buffer, Stream strm, int count)
    {
        if (buffer.Length < count)
            buffer = new byte[count];
        strm.Read(buffer, 0, count);
    }

    public static int ReadUInt16(ref byte[] buffer, Stream strm)
    {
        Read(ref buffer, strm, 2);
        return buffer[0] + (buffer[1] << 8);
    }

    public static uint ReadUInt32(ref byte[] buffer, Stream strm)
    {
        Read(ref buffer, strm, 4);
        return unchecked((uint)(buffer[0] + (buffer[1] << 8) + (buffer[2] << 16) + (buffer[3] << 24)));
    }

    public static string ReadString(ref byte[] buffer, Stream strm, int count)
    {
        Read(ref buffer, strm, count);
        return Encoding.Unicode.GetString(buffer, 0, count);
    }

    public static string ReadStringFull(ref byte[] buffer, Stream strm)
    {
        return ReadString(ref buffer, strm, ReadUInt16(ref buffer, strm));
    }

    public static void WriteUInt16(Stream strm, int uint16)
    {
        strm.Write(new byte[] { (byte)(uint16 & 0xff), (byte)(uint16 >> 8) }, 0, 2);
    }

    public static void WriteUInt32(Stream strm, uint uint32)
    {
        strm.Write(new byte[] {
            (byte)(uint32 & 0xff),
            (byte)((uint32 & 0xff00) >> 8),
            (byte)((uint32 & 0xff0000) >> 16),
            (byte)(uint32 >> 24),
        }, 0, 4);
    }

    public static void WriteString(Stream strm, string str)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(str);
        strm.Write(bytes, 0, bytes.Length);
    }

    public static void WriteStringFull(Stream strm, string str)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(str);

        WriteUInt16(strm, bytes.Length);
        strm.Write(bytes, 0, bytes.Length);
    }
}
