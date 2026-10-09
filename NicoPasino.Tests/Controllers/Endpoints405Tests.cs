using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NicoPasino;

namespace NicoPasino.Tests.Controllers;

/// <summary>
/// Tests de routing (#34, #39): la API no expone DELETE en Productos, Clientes ni
/// Ventas. Como las rutas {recurso}/{id} existen para otros verbos (GET/PATCH), un
/// DELETE responde 405 Method Not Allowed de forma automatica, sin una accion
/// que lo declare.
/// </summary>
public class Endpoints405Tests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public Endpoints405Tests(WebApplicationFactory<Program> factory) {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/ventas/Productos/1")]
    [InlineData("/api/ventas/Clientes/1")]
    [InlineData("/api/ventas/Ventas/1")]
    public async Task Delete_sobre_un_recurso_sin_baja_fisica_devuelve_405(string ruta)
    {
        var respuesta = await _client.DeleteAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
