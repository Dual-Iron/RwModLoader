using Mutator.IO;

namespace Mutator.Web;

static class Cache
{
    public delegate Task<Result<string, E>> GetCacheValueDelegate<E>();

    public static async Task<Result<string, E>> Fetch<E>(string key, GetCacheValueDelegate<E> getValue)
    {
        FileInfo webcache = new(Path.Combine(ExtIO.UserPath, "webcache.dat"));

        if (webcache.CreationTimeUtc < DateTime.UtcNow - new TimeSpan(hours: 1, 0, 0)) {
            webcache.Delete();
        }

        using Stream fs = webcache.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);

        try {
            using BinaryReader reader = new(fs, ExtIO.Enc, true);
            while (fs.Position < fs.Length) {
                string entryKey = reader.ReadString();
                string entryValue = reader.ReadString();

                if (key == entryKey) {
                    return entryValue;
                }
            }
        } catch (EndOfStreamException e) {
            Console.Error.WriteLine("Corrupt webcache! " + e.Message);
            fs.SetLength(0);
        }

        var result = await getValue();

        if (result.MatchSuccess(out var value, out _)) {
            using BinaryWriter writer = new(fs, ExtIO.Enc, true);
            writer.Write(key);
            writer.Write(value);
        }

        return result;
    }
}
