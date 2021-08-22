using System.IO;
using System.Text;

namespace Realm.ModLoading
{
    public sealed class RwmodPool
    {
        public static RwmodPool Fetch()
        {
            string[] files = Directory.GetFiles(Path.Combine(Extensions.UserFolder, "mods"), "*.rwmod", SearchOption.TopDirectoryOnly);

            if (files.Length == 0) return new(new RwmodFile[0]);

            RwmodFile[] rwmodFiles = new RwmodFile[files.Length];

            for (int i = 0; i < files.Length; i++) {
                string path = files[i];
                using Stream fs = File.OpenRead(path);
                rwmodFiles[i] = new(path, Path.GetFileNameWithoutExtension(path), new(fs, Encoding.ASCII));
            }

            return new(rwmodFiles);
        }

        public RwmodPool(RwmodFile[] rwmodFiles)
        {
            RwmodFiles = rwmodFiles;
        }

        public RwmodFile[] RwmodFiles { get; }

        private Execution ExecAll(string arg, int timeout)
        {
            StringBuilder args = new("--parallel");
            foreach (RwmodFile rwmodFile in RwmodFiles) {
                args.Append($" --{arg} \"{rwmodFile.Path}\"");
            }
            return Execution.From(Extensions.MutatorPath, args.ToString(), timeout);
        }

        public Execution UnwrapAll(int timeout = -1) => ExecAll("unwrap", timeout);

        public Execution RestoreAll(int timeout = -1) => ExecAll("restore", timeout);

        public Execution Restore(int index, int timeout = -1)
        {
            return Execution.From(Extensions.MutatorPath, $"--restore \"{RwmodFiles[index]}\"", timeout);
        }
    }
}