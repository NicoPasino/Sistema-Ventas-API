using NicoPasino.Core.Interfaces;

namespace NicoPasino.Tests.Common.Fakes;

/// <summary>
/// <see cref="IGeneradorCodigos"/> determinista: devuelve los valores dados en orden
/// y, agotada la secuencia, repite el minimo del rango. Permite asertar el codigo
/// exacto y contar los reintentos (#44).
/// </summary>
public sealed class GeneradorCodigosFijo : IGeneradorCodigos
{
    private readonly Queue<int> _valores;

    public GeneradorCodigosFijo(params int[] valores) => _valores = new Queue<int>(valores);

    public int Llamadas { get; private set; }

    public int Siguiente(int minimoInclusive, int maximoExclusivo)
    {
        Llamadas++;
        return _valores.Count > 0 ? _valores.Dequeue() : minimoInclusive;
    }
}
