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

            Stream stdout = Console.OpenStandardOutput();
            
            using BinaryWriter writer = new(stdout, InstallerApi.UseEncoding, true);

            foreach (var mod in await ModList.GetMods()) {
                mod.Write(writer);
            }
        }
    }
}
