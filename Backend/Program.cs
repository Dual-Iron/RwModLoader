using Backend;
using Backend.IO;
using Backend.Patching;
using Backend.Web;

if (args.Length == 0) {
    if (Path.GetFileName(Environment.ProcessPath) != "backend.exe") {
        var result = RealmInstaller.UserInstall();
        if (!result.Successful) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.Write("Press any key to exit. ");
            Console.ReadKey(true);

            return (int)result.Code;
        }
    }
    return 0;
}

using var onExit = new Disposable(ExtGlobal.Exit);
using var argEnumerator = ((IEnumerable<string>)args).GetEnumerator();

while (argEnumerator.MoveNext()) {
    ExitStatus status = argEnumerator.Current switch {
        "-?" => PrintHelp(),
        "-i" => RealmInstaller.Install(),
        "-q" => SelfUpdater.QuerySelfUpdate().Result,
        "-dl" => Download(argEnumerator),
        "-rdb" => argEnumerator.MoveNext() ? Downloader.Rdb(argEnumerator.Current).Result : ExitStatus.ExpectedArg,
        "-p" => argEnumerator.MoveNext() ? Patcher.Patch(argEnumerator.Current) : ExitStatus.ExpectedArg,
        "-w" => argEnumerator.MoveNext() ? Wrapper.Wrap(argEnumerator.Current) : ExitStatus.ExpectedArg,
        "-e" => argEnumerator.MoveNext() ? Extractor.Extract(argEnumerator.Current) : ExitStatus.ExpectedArg,
        _ => ExitStatus.UnknownArg
    };

    if (!status.Successful) {
        Console.Error.Write(status);

        if (status.Code is ExitStatus.Codes.UnknownArg or ExitStatus.Codes.ExpectedArg)
            PrintHelp();

        return (int)status.Code;
    }
}

return 0;

static ExitStatus Download(IEnumerator<string> args)
{
    if (args.MoveNext()) {
        string first = args.Current;

        if (args.MoveNext()) {
            return Downloader.Download(first, args.Current).Result;
        }
    }
    return ExitStatus.ExpectedArg;
}

static ExitStatus PrintHelp()
{
    Console.WriteLine();
    Console.WriteLine($@"Backend v{typeof(Program).Assembly.GetName().Version}
-?                 prints this help screen
-i                 installs Realm
-u                 uninstalls Realm
-q                 queries for a self-update and prints 'y' or 'n'
-dl  [url]  [name] downloads the icon at [url] into the file at [name]
-rdb [name]        downloads the specified mod from https://rdb.dual-iron.xyz and installs it
-p   [path]        patches the .dll file at [path]
-w   [path]        wraps the file or directory at [path] into a new RWMOD file and prints the RWMOD's name
-e   [path]        extracts the contents of the .rwmod file at [path] into a new directory
");
    return ExitStatus.Success;
}
