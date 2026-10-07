using Microsoft.AspNetCore.Mvc;

namespace NicoPasino.Tests.Common;

/// <summary>
/// Azucar para afirmar sobre <see cref="IActionResult"/> en los tests de controller.
/// <para>
/// Los endpoints devuelven <c>Ok(new { success = true })</c> o
/// <c>BadRequest(new { message = ... })</c>, o sea objetos anonimos. Los objetos
/// anonimos son <c>internal</c>, asi que <c>dynamic</c> no los puede leer desde el
/// ensamblado de tests (RuntimeBinderException). Por eso se inspeccionan por
/// reflexion con <see cref="Prop"/>.
/// </para>
/// </summary>
internal static class ResultadoHttp
{
    /// <summary>Codigo de estado HTTP del resultado (200 por defecto).</summary>
    public static int Estado(this IActionResult resultado) => resultado switch {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => 200
    };

    /// <summary>Cuerpo del resultado, si lo tiene.</summary>
    public static object? Cuerpo(this IActionResult resultado)
        => (resultado as ObjectResult)?.Value;

    /// <summary>Lee una propiedad publica del cuerpo anonimo (ej. <c>"success"</c>, <c>"message"</c>).</summary>
    public static object? Prop(this IActionResult resultado, string nombre)
    {
        var cuerpo = resultado.Cuerpo();
        return cuerpo?.GetType().GetProperty(nombre)?.GetValue(cuerpo);
    }
}
