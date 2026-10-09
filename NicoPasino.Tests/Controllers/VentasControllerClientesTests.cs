using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NicoPasino.Controllers;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Tests.Common;
using NicoPasino.Tests.Common.Builders;

namespace NicoPasino.Tests.Controllers;

/// <summary>
/// Tests de los 6 endpoints de <c>Ventas.Clientes.cs</c> (#27).
/// <para>
/// Los endpoints GET/POST/PUT se prueban contra sustitutos de
/// <c>IServicioGenerico</c>: solo traducen (resultado | excepcion) a codigo HTTP.
/// El PATCH se prueba contra el <c>ClienteServicio</c> real porque <c>Patch</c> es
/// no-virtual y no se puede sustituir.
/// </para>
/// <para>
/// No hay DELETE: la baja fisica no esta permitida. DELETE /Clientes/{id}
/// responde 405 (ver <c>Endpoints405Tests</c>).
/// </para>
/// </summary>
public class VentasControllerClientesTests
{
    private static ClienteDto DtoCliente(int documento = 30111222, string nombre = "Juan Perez")
        => new() {
            Nombre = nombre,
            Correo = "juan.perez@example.com",
            Documento = documento,
            Telefono = "3514445555",
            Activo = true
        };

    // ================================================================== GET Clientes

    [Fact]
    public async Task GetAll_devuelve_200_con_la_coleccion_del_servicio()
    {
        var h = new VentasControllerHarness();
        var esperado = new[] { DtoCliente() };
        h.ServicioCliente.GetAll(true).Returns(esperado);

        var resultado = await h.Controller.GetAllClientes();

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task GetAll_sin_clientes_devuelve_200_con_coleccion_vacia()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.GetAll(true).Returns([]);

        var resultado = await h.Controller.GetAllClientes();

        resultado.Estado().Should().Be(200);
        ((IEnumerable<ClienteDto>)resultado.Cuerpo()!).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_cuando_el_servicio_falla_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.GetAll(true)
            .Returns(Task.FromException<IEnumerable<ClienteDto>>(new Exception("boom")));

        var resultado = await h.Controller.GetAllClientes();

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500");
    }

    // ========================================================= GET Clientes/search

    [Fact]
    public async Task Search_devuelve_200_con_los_resultados()
    {
        var h = new VentasControllerHarness();
        var esperado = new[] { DtoCliente() };
        h.ServicioCliente.GetAll(Arg.Any<string>(), Arg.Any<string?>()).Returns(esperado);

        var resultado = await h.Controller.GetAllClientes("nombre", "juan");

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task Search_con_campo_no_soportado_devuelve_400_con_el_mensaje()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.GetAll(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<IEnumerable<ClienteDto>>(
                new DataException("Campo de búsqueda 'x' no soportado.")));

        var resultado = await h.Controller.GetAllClientes("x", "v");

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Campo de búsqueda 'x' no soportado.");
    }

    /// <summary>
    /// Con el campo vacio el servicio lanza <c>DataException</c>, que el controller
    /// traduce a 400 con el mensaje de negocio (#36).
    /// </summary>
    [Fact]
    public async Task Search_con_campo_vacio_devuelve_400_con_el_mensaje()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.GetAll(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<IEnumerable<ClienteDto>>(
                new DataException("Campo de búsqueda no válido.")));

        var resultado = await h.Controller.GetAllClientes("", null);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Campo de búsqueda no válido.");
    }

    /// <summary>
    /// Red de seguridad (#36): aunque un servicio lanzara <c>ArgumentException</c>
    /// en vez de <c>DataException</c>, el controller responde 400, no 500.
    /// </summary>
    [Fact]
    public async Task Search_con_ArgumentException_devuelve_400()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.GetAll(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<IEnumerable<ClienteDto>>(
                new ArgumentException("Campo de búsqueda no válido.")));

        var resultado = await h.Controller.GetAllClientes("", null);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Campo de búsqueda no válido.");
    }

    // ============================================================== GET Clientes/{id}

    [Fact]
    public async Task GetCliente_existente_devuelve_200_con_el_cliente()
    {
        var h = new VentasControllerHarness();
        var cliente = DtoCliente();
        h.ServicioCliente.GetById(7).Returns(cliente);

        var resultado = await h.Controller.GetCliente(7);

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(cliente);
    }

    /// <summary>
    /// El servicio devuelve <c>null</c> cuando el cliente no existe, asi que la rama
    /// <c>NotFound</c> responde 404 (#35).
    /// </summary>
    [Fact]
    public async Task GetCliente_inexistente_devuelve_404()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.GetById(9999).Returns(Task.FromResult<ClienteDto>(null!));

        var resultado = await h.Controller.GetCliente(9999);

        resultado.Estado().Should().Be(404);
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCliente_cuando_el_servicio_falla_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.GetById(1).Returns(Task.FromException<ClienteDto>(new Exception("boom")));

        var resultado = await h.Controller.GetCliente(1);

        resultado.Estado().Should().Be(500);
    }

    // ================================================================ POST Clientes

    [Fact]
    public async Task Crear_exitoso_devuelve_200_con_success_true()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Create(Arg.Any<ClienteDto>()).Returns(true);

        var resultado = await h.Controller.CrearCliente(DtoCliente());

        resultado.Estado().Should().Be(200);
        resultado.Prop("success").Should().Be(true);
    }

    [Fact]
    public async Task Crear_con_body_null_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.CrearCliente(null!);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Datos inválidos, por favor revisar.");
    }

    [Fact]
    public async Task Crear_cuando_Create_devuelve_false_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Create(Arg.Any<ClienteDto>()).Returns(false);

        var resultado = await h.Controller.CrearCliente(DtoCliente());

        resultado.Estado().Should().Be(500);
    }

    [Fact]
    public async Task Crear_con_documento_duplicado_devuelve_400()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Create(Arg.Any<ClienteDto>())
            .Returns(Task.FromException<bool>(new DataException("Ya existe un cliente con el documento '30111222'.")));

        var resultado = await h.Controller.CrearCliente(DtoCliente());

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Ya existe un cliente con el documento '30111222'.");
    }

    [Fact]
    public async Task Crear_cuando_el_servicio_lanza_una_excepcion_generica_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Create(Arg.Any<ClienteDto>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("boom")));

        var resultado = await h.Controller.CrearCliente(DtoCliente());

        resultado.Estado().Should().Be(500);
    }

    // ================================================================= PUT Clientes

    [Fact]
    public async Task Actualizar_exitoso_devuelve_200_con_success_true()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Update(Arg.Any<ClienteDto>()).Returns(true);

        var resultado = await h.Controller.ActualizarCliente(DtoCliente());

        resultado.Estado().Should().Be(200);
        resultado.Prop("success").Should().Be(true);
    }

    [Fact]
    public async Task Actualizar_con_body_null_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.ActualizarCliente(null!);

        resultado.Estado().Should().Be(400);
    }

    [Fact]
    public async Task Actualizar_cuando_Update_devuelve_false_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Update(Arg.Any<ClienteDto>()).Returns(false);

        var resultado = await h.Controller.ActualizarCliente(DtoCliente());

        resultado.Estado().Should().Be(500);
    }

    [Fact]
    public async Task Actualizar_con_UpdateException_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Update(Arg.Any<ClienteDto>())
            .Returns(Task.FromException<bool>(new UpdateException("No se pudo actualizar en la base de datos.")));

        var resultado = await h.Controller.ActualizarCliente(DtoCliente());

        resultado.Estado().Should().Be(500);
    }

    [Fact]
    public async Task Actualizar_con_DataException_devuelve_400()
    {
        var h = new VentasControllerHarness();
        h.ServicioCliente.Update(Arg.Any<ClienteDto>())
            .Returns(Task.FromException<bool>(new DataException("Objeto original no encontrado.")));

        var resultado = await h.Controller.ActualizarCliente(DtoCliente());

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Objeto original no encontrado.");
    }

    // =============================================================== PATCH Clientes
    // Aca se usa el ClienteServicio real (Patch es no-virtual).

    [Fact]
    public async Task Patch_exitoso_actualiza_el_cliente_y_devuelve_200()
    {
        var h = new VentasControllerHarness();
        var cliente = h.SembrarCliente();

        var resultado = await h.Controller.ActualizarClienteParcial(
            cliente.Documento, new ClientePatchDto { Nombre = "Jorge Gomez" });

        resultado.Estado().Should().Be(200);
        resultado.Prop("success").Should().Be(true);
        h.RepoCliente.Entidades.Single().Nombre.Should().Be("Jorge Gomez");
    }

    [Fact]
    public async Task Patch_sin_datos_devuelve_400()
    {
        var h = new VentasControllerHarness();
        var cliente = h.SembrarCliente();

        var resultado = await h.Controller.ActualizarClienteParcial(cliente.Documento, new ClientePatchDto());

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("No se recibieron datos para actualizar.");
    }

    [Fact]
    public async Task Patch_con_body_null_devuelve_400()
    {
        var h = new VentasControllerHarness();
        var cliente = h.SembrarCliente();

        var resultado = await h.Controller.ActualizarClienteParcial(cliente.Documento, null!);

        resultado.Estado().Should().Be(400);
    }

    [Fact]
    public async Task Patch_con_documento_invalido_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.ActualizarClienteParcial(
            123, new ClientePatchDto { Nombre = "Jorge Gomez" });

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("El documento debe tener 8 dígitos.");
    }

    [Fact]
    public async Task Patch_de_un_cliente_inexistente_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.ActualizarClienteParcial(
            30111222, new ClientePatchDto { Nombre = "Jorge Gomez" });

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Cliente no encontrado.");
    }

    [Fact]
    public async Task Patch_con_un_correo_que_ya_usa_otro_cliente_devuelve_400()
    {
        var h = new VentasControllerHarness();
        var a = h.SembrarCliente(ClienteBuilder.Uno().ConDocumento(30111222).ConCorreo("a@example.com"));
        h.SembrarCliente(ClienteBuilder.Uno().ConDocumento(30222333).ConCorreo("b@example.com"));

        var resultado = await h.Controller.ActualizarClienteParcial(
            a.Documento, new ClientePatchDto { Correo = "b@example.com" });

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Ya existe un cliente con el correo 'b@example.com'.");
    }

    // No hay tests de DELETE Clientes: el endpoint se elimino y DELETE
    // /Clientes/{id} responde 405 (ver Endpoints405Tests).
}
