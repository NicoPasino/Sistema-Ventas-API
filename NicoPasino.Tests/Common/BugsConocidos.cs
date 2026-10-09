namespace NicoPasino.Tests.Common;

/// <summary>
/// Catalogo de los bugs conocidos que los tests de caracterizacion documentan.
/// <para>
/// <b>Regla de la casa</b> (#26): un test nunca se deja en <c>Skip</c> para tapar un
/// bug. En cambio, se escribe un test normal que afirma el comportamiento
/// <i>actual</i> (aunque sea incorrecto) y se referencia el issue con un comentario
/// <c>// BUG: ver issue #N</c> usando las constantes de <see cref="Issue*"/>.
/// </para>
/// <para>
/// Cuando el bug se corrija, ese test falla y senala exactamente donde tocar. Un
/// test en <c>Skip</c>, en cambio, deja el bug invisible y el arreglo sin red de
/// seguridad.
/// </para>
/// </summary>
public static class BugsConocidos
{
    // ------------------------------------------------------------ descripciones
    // El texto describe el comportamiento observado (el bug), no el deseado.

    public const string VentaCreateSinTransaccion =
        "VentaServicio.Create no usa transaccion: la venta y el descuento de stock pueden quedar a medias";

    public const string RepositorioAsNoTrackingAsimetrico =
        "RepositorioGenericoVentas.GetAsync aplica AsNoTracking() solo cuando hay filtro; ListarAsync nunca";

    // ------------------------------------------------------------ numeros de issue

    public const int IssueVentaCreateSinTransaccion = 41;
    public const int IssueAsNoTrackingInconsistente = 48;

    /// <summary>
    /// Sufijo autodocumentado para el nombre de un test de caracterizacion.
    /// Ej.: <c>Sufijo("ProductoServicio_ignora_estado")</c> devuelve
    /// <c>"__BUG_ProductoServicio_ignora_estado"</c>.
    /// </summary>
    public static string Sufijo(string slug) => $"__BUG_{slug}";
}
