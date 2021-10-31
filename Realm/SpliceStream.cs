namespace Realm;

sealed class SpliceStream : Stream
{
    private readonly Stream stream;
    private readonly long start;
    private long length;

    public SpliceStream(Stream stream, long offset, long length)
    {
        if (!stream.CanSeek) {
            throw new ArgumentException("Stream must be capable of seeking.");
        }

        start = offset;
        this.stream = stream;
        this.length = length;
    }

    public override bool CanSeek => true;
    public override bool CanRead => stream.CanRead;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => length;
    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Position >= length)
            return 0;

        if (count > length - Position)
            count = (int)(length - Position);

        long realPos = Position + start;

        if (stream.Position != realPos) {
            stream.Position = realPos;
        }

        int read = stream.Read(buffer, offset, count);

        Position = stream.Position - start;

        return read;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        long realPos = Position + start;

        if (stream.Position != realPos) {
            stream.Position = realPos;
        }

        stream.Write(buffer, offset, count);

        Position = stream.Position - start;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return origin switch {
            SeekOrigin.Begin => Position = offset,
            SeekOrigin.End => Position = length - offset,
            SeekOrigin.Current => Position += offset,
            _ => throw new InvalidOperationException()
        };
    }

    public override void SetLength(long value)
    {
        length = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            stream.Dispose();
        }
    }

    public override void Flush() => stream.Flush();
    public override void Close() => stream.Close();
    public override bool CanTimeout => stream.CanTimeout;
    public override int ReadTimeout { get => stream.ReadTimeout; set => stream.ReadTimeout = value; }
    public override int WriteTimeout { get => stream.WriteTimeout; set => stream.WriteTimeout = value; }
}
