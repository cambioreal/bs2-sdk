namespace CambioReal.Bs2.Tests.Fakes;

/// <summary>Relógio controlado pelo teste.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset current = now;

    public override DateTimeOffset GetUtcNow() => current;

    public void Advance(TimeSpan delta) => current += delta;
}
