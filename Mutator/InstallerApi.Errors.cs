using Mono.Cecil;

namespace Mutator;

public static partial class InstallerApi
{
    public static Exception Err(ExitCodes exitCode) => new BadExecutionException(exitCode);
    public static Exception Err(ExitCodes exitCode, string param) => new BadExecutionException(exitCode, $"{exitCode}: {param}.");
    public static Exception ErrFileNotFound(string path) => new BadExecutionException(ExitCodes.AbsentFile, $"The file \"{Path.GetFullPath(path)}\" did not exist.");
    public static Exception ErrAbsentDependency(string filename, AssemblyResolutionException e) 
        => new BadExecutionException(ExitCodes.AbsentDependency, $"The assembly \"{filename}\" could not resolve a reference to \"{e.AssemblyReference}\". Notify the mod author.");
}
