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

        private ProcessResult ExecAll(string arg, int timeout)
        {
            StringBuilder args = new("--parallel");
            foreach (RwmodFile rwmodFile in RwmodFiles) {
                args.Append($" --{arg} \"{rwmodFile.Path}\"");
            }
            return ProcessResult.From(Extensions.MutatorPath, args.ToString(), timeout);
        }

        public ProcessResult UnwrapAll(int timeout = -1) => ExecAll("unwrap", timeout);

        public ProcessResult RestoreAll(int timeout = -1) => ExecAll("restore", timeout);

        public ProcessResult Restore(int index, int timeout = -1)
        {
            return ProcessResult.From(Extensions.MutatorPath, $"--restore \"{RwmodFiles[index]}\"", timeout);
        }
    }
}