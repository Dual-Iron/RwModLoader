using System.Diagnostics.CodeAnalysis;

namespace Rwml;

readonly struct Result<T, E>
{
    private readonly T? value;
    private readonly E? error;
    private readonly bool success;

    private Result(T? value, E? error, bool success)
    {
        this.value = value;
        this.error = error;
        this.success = success;
    }

    public Result(T value) : this(value, default, true) { }
    public Result(E error) : this(default, error, false) { }

    public readonly bool MatchSuccess([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out E error)
    {
        value = this.value;
        error = this.error;
        return success;
    }

    public readonly bool MatchFailure([MaybeNullWhen(true)] out T value, [MaybeNullWhen(false)] out E error)
    {
        value = this.value;
        error = this.error;
        return !success;
    }

    public static implicit operator Result<T, E>(T value) => new(value);
    public static implicit operator Result<T, E>(E error) => new(error);
}
