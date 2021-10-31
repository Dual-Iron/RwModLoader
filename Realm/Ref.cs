namespace Realm;

sealed class Ref<T>
{
    public Ref(T value)
    {
        Value = value;
    }

    public T Value { get; }

    public static implicit operator T(Ref<T> r) => r.Value;

    public override string ToString() => $"&{typeof(T)}";
}
