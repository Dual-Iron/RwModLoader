namespace Realm.AssemblyLoading;

[Serializable]
sealed class ProgramRunException : Exception
{
    public ProgramRunException(string message) : base(message) { }
}
