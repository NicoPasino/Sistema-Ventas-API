using FluentAssertions;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de los guardas de constructor de los tres servicios (#14 a #23). Los tres
/// servicios hacen <c>repo ?? throw new ArgumentNullException(...)</c> en el
/// constructor: sin esto, un <c>null</c> pasaria y reventaria mas adentro, con un
/// <c>NullReferenceException</c> sin contexto.
/// </summary>
public class ServiciosConstructorTests
{
    [Fact]
    public void ProductoServicio_rechaza_un_repositorio_null()
    {
        var excepcion = Assert.Throws<ArgumentNullException>(
            () => new ProductoServicio(null!, new ProductoValidador(new RepositorioGenericoVentasFake<Categoria>())));

        excepcion.ParamName.Should().Be("repoG");
    }

    [Fact]
    public void ProductoServicio_rechaza_un_validador_null()
    {
        var excepcion = Assert.Throws<ArgumentNullException>(
            () => new ProductoServicio(new RepositorioGenericoVentasFake<Producto>(), null!));

        excepcion.ParamName.Should().Be("validador");
    }

    [Fact]
    public void ClienteServicio_rechaza_un_repositorio_null()
    {
        var excepcion = Assert.Throws<ArgumentNullException>(
            () => new ClienteServicio(null!, new ClienteValidador(new RepositorioGenericoVentasFake<Cliente>())));

        excepcion.ParamName.Should().Be("repoG");
    }

    [Fact]
    public void ClienteServicio_rechaza_un_validador_null()
    {
        var excepcion = Assert.Throws<ArgumentNullException>(
            () => new ClienteServicio(new RepositorioGenericoVentasFake<Cliente>(), null!));

        excepcion.ParamName.Should().Be("validador");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void VentaServicio_rechaza_cualquiera_de_sus_cinco_dependencias_null(int indice)
    {
        var repo = new RepositorioGenericoVentasFake<Venta>();
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>();
        var repoProducto = new RepositorioGenericoVentasFake<Producto>();
        var repoVpp = new RepositorioGenericoVentasFake<Ventaporproducto>();
        var validador = new VentaValidador(repoCliente, repoProducto);

        var excepcion = Assert.Throws<ArgumentNullException>(() => new VentaServicio(
            indice == 1 ? null! : repo,
            indice == 2 ? null! : repoCliente,
            indice == 3 ? null! : repoProducto,
            indice == 4 ? null! : repoVpp,
            indice == 5 ? null! : validador));

        excepcion.ParamName.Should().Be(
            indice switch { 1 => "repoG", 2 => "repoCliente", 3 => "repoProducto", 4 => "repoVpp", _ => "validador" });
    }

    // ------------------------------------------------------------------ repositorio que devuelve null

    /// <summary>
    /// Los tres <c>GetAll(bool)</c> Shieldean con <c>objsDb != null &amp;&amp; objsDb.Any()</c>.
    /// Esta rama solo es alcanzable con un repositorio que devuelva <c>null</c> en vez
    /// de una coleccion vacia, que es justo lo que hace el fake cuando se le inyecta
    /// un delegate. Caracteriza que no se rompe y devuelve vacio.
    /// </summary>
    [Fact]
    public async Task GetAll_de_Producto_aguanta_un_repositorio_que_devuelve_null()
    {
        var repo = new RepositorioGenericoVentasFake<Producto>();
        repo.AlListarAsync = () => null!;
        var servicio = new ProductoServicio(repo,
            new ProductoValidador(new RepositorioGenericoVentasFake<Categoria>()));

        var resultado = await servicio.GetAll(true);

        resultado.Should().NotBeNull();
        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_de_Cliente_aguanta_un_repositorio_que_devuelve_null()
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>();
        repo.AlListarAsync = () => null!;
        var servicio = new ClienteServicio(repo, new ClienteValidador(repo));

        (await servicio.GetAll(true)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_de_Venta_aguanta_un_repositorio_que_devuelve_null()
    {
        var repo = new RepositorioGenericoVentasFake<Venta>();
        repo.AlListarAsync = () => null!;
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>();
        var repoProducto = new RepositorioGenericoVentasFake<Producto>();
        var servicio = new VentaServicio(repo, repoCliente, repoProducto,
            new RepositorioGenericoVentasFake<Ventaporproducto>(),
            new VentaValidador(repoCliente, repoProducto));

        (await servicio.GetAll(true)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_con_campo_aguanta_un_repositorio_que_devuelve_null()
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>();
        repo.AlListarAsync = () => null!;
        var servicio = new ClienteServicio(repo, new ClienteValidador(repo));

        (await servicio.GetAll("nombre", "ana")).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_con_campo_de_Producto_aguanta_un_repositorio_que_devuelve_null()
    {
        var repo = new RepositorioGenericoVentasFake<Producto>();
        repo.AlListarAsync = () => null!;
        var servicio = new ProductoServicio(repo,
            new ProductoValidador(new RepositorioGenericoVentasFake<Categoria>()));

        (await servicio.GetAll("nombre", "gaseosa")).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_con_campo_de_Venta_aguanta_un_repositorio_que_devuelve_null()
    {
        var repo = new RepositorioGenericoVentasFake<Venta>();
        repo.AlListarAsync = () => null!;
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>();
        var repoProducto = new RepositorioGenericoVentasFake<Producto>();
        var servicio = new VentaServicio(repo, repoCliente, repoProducto,
            new RepositorioGenericoVentasFake<Ventaporproducto>(),
            new VentaValidador(repoCliente, repoProducto));

        (await servicio.GetAll("nombre", "ana")).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ armadores usados por los tests

    [Fact]
    public void Los_builders_generan_clientes_validos_por_defecto()
    {
        // Guarda contra regresiones del builder: si el default se rompe, los 540
        // tests de F3 empiezan a fallar por motivos difíciles de leer.
        var cliente = ClienteBuilder.Uno().Build();

        cliente.Documento.Should().Be(30111222);
        cliente.Nombre.Should().NotBeNullOrWhiteSpace();
        cliente.Correo.Should().NotBeNullOrWhiteSpace();
    }
}