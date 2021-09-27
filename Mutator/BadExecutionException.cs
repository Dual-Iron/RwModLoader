namespace Mutator;

[Serializable]
public class BadExecutionException : Exception
{
    public ExitCodes ExitCode { get; }

    public BadExecutionException(ExitCodes exitCode) : this(exitCode, $"{exitCode}.") { }

    public BadExecutionException(ExitCodes exitCode, string message) : base(message)
    {
        ExitCode = exitCode;
    }

    protected BadExecutionException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}
