using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NicoPasino.Controllers;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Controllers;

/// <summary>
/// Tests de los 5 endpoints activos de <c>Ventas.Ventas.cs</c> (#28). El
/// <c>PUT Ventas</c> esta comentado en el controller, asi que no se prueba.
/// <para>
/// Los dos endpoints rotos que este archivo documenta:
/// <list type="bullet">
///   <item><description><c>GET Ventas/{id}</c> nunca devuelve 404
///   (<see cref="BugsConocidos.IssueVentaGetByIdNunca404"/>);</description></item>
///   <item><description><c>DELETE Ventas/{id}</c> siempre devuelve 500 porque
///   <c>VentaServicio.Enable</c> lanza <c>NotImplementedException</c>
///   (<see cref="BugsConocidos.IssueEliminarVentaSiempre500"/>).</description></item>
/// </list>
/// </para>
/// </summary>
public class VentasControllerVentasTests
{
    private static VentaDetalleDto Detalle(int numero = 1234567)
        => new() { Numero = numero, Detalle = "Compra", Total = 1500m };

    // ================================================================== GET Ventas

    [Fact]
    public async Task GetAll_devuelve_200_con_la_coleccion_del_servicio()
    {
        var h = new VentasControllerHarness();
        var esperado = new[] { Detalle() };
        h.ServicioVenta.GetAll(true).Returns(esperado);

        var resultado = await h.Controller.GetAllVentas();

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task GetAll_sin_ventas_devuelve_200_vacio()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.GetAll(true).Returns([]);

        var resultado = await h.Controller.GetAllVentas();

        resultado.Estado().Should().Be(200);
        ((IEnumerable<VentaDetalleDto>)resultado.Cuerpo()!).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_cuando_el_servicio_falla_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.GetAll(true)
            .Returns(Task.FromException<IEnumerable<VentaDetalleDto>>(new Exception("boom")));

        var resultado = await h.Controller.GetAllVentas();

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500");
    }

    // ========================================================== GET Ventas/search

    [Fact]
    public async Task Search_devuelve_200_con_los_resultados()
    {
        var h = new VentasControllerHarness();
        var esperado = new[] { Detalle() };
        h.ServicioVenta.GetAll(Arg.Any<string>(), Arg.Any<string?>()).Returns(esperado);

        var resultado = await h.Controller.GetAllVentas("numero", "123");

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task Search_con_campo_no_soportado_devuelve_400()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.GetAll(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<IEnumerable<VentaDetalleDto>>(
                new DataException("Campo de búsqueda 'x' no soportado.")));

        var resultado = await h.Controller.GetAllVentas("x", "v");

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Campo de búsqueda 'x' no soportado.");
    }

    /// <summary>Caracteriza el bug #36 en el endpoint de ventas: 500 en vez de 400.</summary>
    [Fact]
    public async Task Search_con_campo_vacio_devuelve_500_en_vez_de_400__BUG_ArgumentException_no_catcheada()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.GetAll(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<IEnumerable<VentaDetalleDto>>(
                new ArgumentException("Campo de búsqueda no válido.")));

        var resultado = await h.Controller.GetAllVentas("", null);

        resultado.Estado().Should().Be(500, BugsConocidos.BusquedaConCampoVacioDevuelve500);
    }

    // =============================================================== GET Ventas/{id}

    [Fact]
    public async Task GetVenta_existente_devuelve_200_con_el_detalle()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.GetById(5).Returns(Detalle());

        var resultado = await h.Controller.GetVenta(5);

        resultado.Estado().Should().Be(200);
    }

    /// <summary>
    /// Caracteriza el bug #38: el <c>NotFound</c> esta comentado y <c>GetById</c>
    /// devuelve un DTO vacio, asi que nunca se responde 404.
    /// </summary>
    [Fact]
    public async Task GetVenta_inexistente_devuelve_200_y_nunca_404__BUG_rama_404_comentada()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.GetById(9999).Returns(new VentaDetalleDto());

        var resultado = await h.Controller.GetVenta(9999);

        resultado.Estado().Should().Be(200, BugsConocidos.VentaGetByIdNuncaDevuelve404);
        resultado.Should().NotBeOfType<NotFoundObjectResult>();
    }

    // ================================================================ POST Ventas

    [Fact]
    public async Task Crear_exitoso_devuelve_200_con_ok_y_success()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.Create(Arg.Any<VentaDto>()).Returns(true);

        var resultado = await h.Controller.CrearVenta(new VentaDtoBuilder().Build());

        resultado.Estado().Should().Be(200);
        resultado.Prop("ok").Should().Be(true);
        resultado.Prop("success").Should().Be(true);
    }

    [Fact]
    public async Task Crear_con_body_null_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.CrearVenta(null!);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Datos inválidos, por favor revisar.");
    }

    [Fact]
    public async Task Crear_cuando_Create_devuelve_false_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.Create(Arg.Any<VentaDto>()).Returns(false);

        var resultado = await h.Controller.CrearVenta(new VentaDtoBuilder().Build());

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500");
    }

    [Fact]
    public async Task Crear_con_DataException_devuelve_400_con_el_mensaje()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.Create(Arg.Any<VentaDto>())
            .Returns(Task.FromException<bool>(new DataException("Stock insuficiente.")));

        var resultado = await h.Controller.CrearVenta(new VentaDtoBuilder().Build());

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Stock insuficiente.");
    }

    /// <summary>
    /// Tercera rama de POST Ventas: <c>UpdateException</c> responde 500 devolviendo
    /// el <b>mensaje crudo</b> de la excepcion (a diferencia de la rama generica,
    /// que devuelve un string fijo).
    /// </summary>
    [Fact]
    public async Task Crear_con_UpdateException_devuelve_500_con_el_mensaje_crudo()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.Create(Arg.Any<VentaDto>())
            .Returns(Task.FromException<bool>(new UpdateException("No se pudo actualizar el stock del producto.")));

        var resultado = await h.Controller.CrearVenta(new VentaDtoBuilder().Build());

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error interno (500): No se pudo actualizar el stock del producto.");
    }

    /// <summary>
    /// Cuarta rama: cualquier otra excepcion responde 500 con un string generico,
    /// sin filtrar el detalle interno.
    /// </summary>
    [Fact]
    public async Task Crear_con_una_excepcion_generica_devuelve_500_con_string_generico()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.Create(Arg.Any<VentaDto>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("detalle interno secreto")));

        var resultado = await h.Controller.CrearVenta(new VentaDtoBuilder().Build());

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500");
        resultado.Cuerpo()!.ToString().Should().NotContain("secreto");
    }

    // ============================================================= DELETE Ventas

    /// <summary>
    /// Caracteriza el bug #37 con el <c>VentaServicio</c> <b>real</b>: <c>Enable</c>
    /// lanza <c>NotImplementedException</c> y el controller la traduce a 500. No hay
    /// forma de obtener un 200 en este endpoint.
    /// </summary>
    [Fact]
    public async Task Eliminar_siempre_devuelve_500_porque_Enable_no_esta_implementado__BUG()
    {
        var repoVenta = new RepositorioGenericoVentasFake<Venta>();
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>();
        var repoProducto = new RepositorioGenericoVentasFake<Producto>();
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>();
        var ventaReal = new VentaServicio(
            repoVenta, repoCliente, repoProducto,
            new RepositorioGenericoVentasFake<Ventaporproducto>(),
            new VentaValidador(repoCliente, repoProducto));

        var controller = new VentasController(
            Substitute.For<IServicioGenerico<Producto, ProductoDto, ProductoDto>>(),
            ventaReal,
            Substitute.For<IServicioGenerico<Cliente, ClienteDto, ClienteDto>>(),
            Substitute.For<IServicioGenerico<Categoria, CategoriaDto, CategoriaDto>>(),
            new ProductoServicio(repoProducto, new ProductoValidador(repoCategoria)),
            new ClienteServicio(repoCliente, new ClienteValidador(repoCliente)));

        var resultado = await controller.EliminarVenta(1);

        resultado.Estado().Should().Be(500, BugsConocidos.EliminarVentaSiempreDevuelve500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500");
    }

    [Fact]
    public async Task Eliminar_cuando_el_servicio_lanza_una_excepcion_generica_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioVenta.Enable(Arg.Any<int>(), false)
            .Returns(Task.FromException<bool>(new InvalidOperationException("boom")));

        var resultado = await h.Controller.EliminarVenta(1);

        resultado.Estado().Should().Be(500);
    }
}
