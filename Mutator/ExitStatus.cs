namespace Mutator;

readonly struct ExitStatus
{
    public enum Codes
    {
        Success = 0x00,
        UnknownArg = 0x10,
        ExpectedArg,
        InvalidTagName,
        CorruptRwmod,
        FileNotFound = 0x20,
        FolderNotFound,
        RwFolderNotFound,
        RwPathInvalid,
        IOError = 0x30,
        ConnectionFailed
    }

    public readonly Codes Code;
    public readonly string? Message;

    private ExitStatus(Codes code, string? message = null)
    {
        Code = code;
        Message = message;
    }

    public readonly bool Successful => Code == Codes.Success;

    public readonly override string? ToString()
    {
        if (string.IsNullOrEmpty(Message)) {
            return Code.ToString();
        }
        return $"{Code}: {Message}";
    }

    public static ExitStatus Success => default;
    public static ExitStatus UnknownArg => new(Codes.UnknownArg);
    public static ExitStatus ExpectedArg => new(Codes.ExpectedArg);
    public static ExitStatus ConnectionFailed => new(Codes.ConnectionFailed);
    public static ExitStatus InvalidTagName => new(Codes.InvalidTagName);
    public static ExitStatus FileNotFound(string path) => new(Codes.FileNotFound, $"file \"{path}\" not found");
    public static ExitStatus FolderNotFound(string path) => new(Codes.FolderNotFound, $"folder \"{path}\" not found");
    public static ExitStatus CorruptRwmod(string name) => new(Codes.CorruptRwmod, $"rwmod \"{name}\" is corrupt");
    public static ExitStatus RwFolderNotFound =>
        new(Codes.RwFolderNotFound, $"couldn't find the Rain World directory; create a \"path.txt\" file in \"{Path.GetDirectoryName(Environment.ProcessPath)}\" that contains the path to the Rain World folder");
    public static ExitStatus RwPathInvalid =>
        new(Codes.RwPathInvalid, $"the file \"path.txt\" doesn't contain the path to the Rain World folder");
    public static ExitStatus IOError(string message) => new(Codes.IOError, $"an IO error occurred; message: {message}");
}
