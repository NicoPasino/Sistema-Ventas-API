using NicoPasino.Core.Interfaces;

namespace NicoPasino.Servicios.Servicios
{
    /// <summary>
    /// Implementacion por defecto de <see cref="IGeneradorCodigos"/> basada en
    /// <see cref="Random.Shared"/>, que es thread-safe y no crea una instancia nueva
    /// por llamada (a diferencia de <c>new Random()</c>).
    /// </summary>
    public sealed class GeneradorCodigos : IGeneradorCodigos
    {
        public int Siguiente(int minimoInclusive, int maximoExclusivo)
            => Random.Shared.Next(minimoInclusive, maximoExclusivo);
    }
}
