namespace NicoPasino.Tests.Common.Fakes;

/// <summary>
/// <see cref="TimeProvider"/> con un instante fijo, para asertar timestamps exactos
/// sin ventanas de tolerancia. Se inyecta en los servicios y validadores (#44).
/// </summary>
public sealed class RelojFijo : TimeProvider
{
    private DateTimeOffset _ahora;

    public RelojFijo(DateTimeOffset ahora) => _ahora = ahora;

    public override DateTimeOffset GetUtcNow() => _ahora;

    public void Avanzar(TimeSpan delta) => _ahora = _ahora.Add(delta);
}
