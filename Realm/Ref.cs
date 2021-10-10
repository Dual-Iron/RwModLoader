namespace Realm;

public sealed class Ref<T>
{
    public Ref(T value)
    {
        Value = value;
    }

    public T Value { get; }

    public static implicit operator T(Ref<T> r) => r.Value;

    public override string ToString() => $"&{typeof(T)}";
}

public sealed class MutRef<T>
{
    private T value;

    public MutRef(T value)
    {
        this.value = value;
    }

    public ref T Value => ref value;

    public static implicit operator T(MutRef<T> r) => r.Value;

    public override string ToString() => $"&mut {typeof(T)}";
}
