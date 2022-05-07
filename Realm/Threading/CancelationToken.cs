namespace Realm.Threading;

sealed class CancelationSource
{
    private bool canceled;

    public bool Canceled => canceled;

    public void Cancel()
    {
        canceled = true;
    }

    public CancelationToken Token => new(this);
}

struct CancelationToken
{
    private readonly CancelationSource? source;

    public bool Canceled => source?.Canceled ?? false;

    public CancelationToken(CancelationSource source)
    {
        this.source = source;
    }
}
