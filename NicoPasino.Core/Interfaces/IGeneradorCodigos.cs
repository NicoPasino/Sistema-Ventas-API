namespace NicoPasino.Core.Interfaces
{
    /// <summary>
    /// Fuente de numeros pseudoaleatorios para los codigos publicos (IdPublica de
    /// producto e Numero de venta). Se abstrae detras de una interfaz para poder
    /// inyectar un generador determinista en los tests y afirmar el valor exacto.
    /// </summary>
    public interface IGeneradorCodigos
    {
        /// <summary>
        /// Devuelve un entero en el rango <c>[minimoInclusive, maximoExclusivo)</c>.
        /// </summary>
        int Siguiente(int minimoInclusive, int maximoExclusivo);
    }
}
