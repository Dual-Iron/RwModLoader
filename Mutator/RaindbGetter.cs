using System;
using System.IO;
using System.Threading.Tasks;

namespace Mutator
{
    public static class RaindbGetter
    {
        public static async Task Run()
        {
            await InstallerApi.VerifyInternetConnection();

            using var stdout = new MemoryStream();

            foreach (var mod in await ModList.GetMods()) {
                var order = new[]
                {
                    mod.Repo,
                    mod.Author,
                    mod.Name,
                    mod.Description,
                    mod.IconUrl,
                    mod.VideoUrl,
                    mod.ModDependencies
                };

                for (int i = 0; i < order.Length; i++) {
                    await stdout.WriteAsync(InstallerApi.UseEncoding.GetBytes(order[i] ?? ""));
                }
            }

            await stdout.CopyToAsync(Console.OpenStandardOutput());
        }
    }
}
