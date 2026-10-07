using FluentAssertions;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de lectura de <c>VentaServicio</c> (#21): <c>GetAll(bool)</c>,
/// <c>GetAll(campo, valor)</c> y <c>GetById</c>.
/// </summary>
public class VentaServicioLecturaTests
{
    private static Cliente ClientePorDefecto => ClienteBuilder.Uno()
        .ConId(1).ConDocumento(30111222).ConNombre("Ana Gomez").ConCorreo("ana@correo.com").Build();

    private static Venta Venta(int id, int numero, string? detalle = "Compra",
        DateTime? fechaVenta = null, Cliente? cliente = null, Ventaporproducto[] items = null!)
        => VentaBuilder.Uno()
            .ConId(id)
            .ConNumero(numero)
            .ConDetalle(detalle)
            .ConFechaVenta(fechaVenta ?? new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc))
            .ConCliente(cliente ?? ClienteBuilder.Uno()
                .ConId(1).ConDocumento(30111222).ConNombre("Ana Gomez").ConCorreo("ana@correo.com").Build())
            .ConItems(items ?? [Item(3, 1500.50m)])
            .Build();

    private static Ventaporproducto Item(int cantidad, decimal precio = 1500.50m, string nombre = "Gaseosa 500ml")
        => VentaporproductoBuilder.Uno().ConCantidad(cantidad).ConPrecioUnitario(precio, nombre).Build();

    private static (VentaServicio Servicio, RepositorioGenericoVentasFake<Venta> Repo) CrearCon(params Venta[] ventas)
    {
        var repo = new RepositorioGenericoVentasFake<Venta>(ventas);
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>(ClientePorDefecto);
        var repoProducto = new RepositorioGenericoVentasFake<Producto>();
        var repoVpp = new RepositorioGenericoVentasFake<Ventaporproducto>();
        var servicio = new VentaServicio(repo, repoCliente, repoProducto, repoVpp,
            new VentaValidador(repoCliente, repoProducto));
        return (servicio, repo);
    }

    // ------------------------------------------------------------------ GetAll(bool)

    [Fact]
    public async Task GetAll_devuelve_las_ventas()
    {
        var (servicio, _) = CrearCon(Venta(1, 100), Venta(2, 200));

        (await servicio.GetAll(true)).Should().HaveCount(2);
    }

    /// <summary>
    /// Caracteriza que el parametro <c>activo</c> es inerte: la linea 35 tiene el
    /// filtro comentado. A diferencia de Producto y Cliente, aca no es un bug grave
    /// porque la tabla <c>venta</c> no tiene columna <c>activo</c> (ver el comentario
    /// de <c>Enable</c>), pero la firma sigue prometiendo un filtro que no existe.
    /// </summary>
    [Fact]
    public async Task GetAll_ignora_el_parametro_activo()
    {
        var (servicio, repo) = CrearCon(Venta(1, 100), Venta(2, 200));

        (await servicio.GetAll(true)).Should().HaveCount(2);
        (await servicio.GetAll(false)).Should().HaveCount(2);
        repo.UltimoFiltro.Should().BeNull();
    }

    [Fact]
    public async Task GetAll_ordena_por_fecha_de_venta_descendente()
    {
        var (servicio, _) = CrearCon(
            Venta(1, 100, fechaVenta: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Venta(2, 200, fechaVenta: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Venta(3, 300, fechaVenta: new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var resultado = await servicio.GetAll(true);

        resultado.Select(v => v.Numero).Should().Equal(200, 300, 100);
    }

    [Fact]
    public async Task GetAll_carga_las_tres_navegaciones()
    {
        var (servicio, repo) = CrearCon(Venta(1, 100));

        await servicio.GetAll(true);

        repo.UltimoInclude.Should().Be("IdClienteNavigation.Venta,Ventaporproducto,Ventaporproducto.IdProductoNavigation");
    }

    [Fact]
    public async Task GetAll_proyecta_el_cliente_anidado()
    {
        var (servicio, _) = CrearCon(Venta(1, 100));

        var resultado = await servicio.GetAll(true);

        var venta = resultado.Single();

        venta.Cliente.Should().NotBeNull();
        venta.Cliente!.Documento.Should().Be(30111222);
        venta.Cliente.Nombre.Should().Be("Ana Gomez");
    }

    [Fact]
    public async Task GetAll_proyecta_los_items_con_el_nombre_y_el_subtotal()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, items: [Item(3, 1500.50m, "Gaseosa 500ml")]));

        var item = (await servicio.GetAll(true)).Single().Productos!.Single();

        item.Producto.Should().Be("Gaseosa 500ml", "usa NombreProducto del snapshot");
        item.Cantidad.Should().Be(3);
        item.PrecioUnitario.Should().Be(1500.50m);
        item.SubTotal.Should().Be(4501.50m);
    }

    [Fact]
    public async Task GetAll_calcula_el_total_como_la_suma_de_los_subtotales()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, items: [
            Item(2, 1000m, "A"),
            Item(3, 500m, "B")
        ]));

        var venta = (await servicio.GetAll(true)).Single();

        venta.Total.Should().Be(3500m, "2 x 1000 + 3 x 500");
    }

    [Fact]
    public async Task GetAll_una_venta_sin_items_devuelve_total_cero_y_productos_vacios()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, items: []));

        var venta = (await servicio.GetAll(true)).Single();

        venta.Total.Should().Be(0m);
        venta.Productos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_sin_ventas_devuelve_una_coleccion_vacia()
    {
        var (servicio, _) = CrearCon();

        var resultado = await servicio.GetAll(true);

        resultado.Should().NotBeNull();
        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_propaga_la_excepcion_del_repositorio_sin_envolverla()
    {
        var (servicio, repo) = CrearCon(Venta(1, 100));
        repo.AlListarAsync = () => throw new InvalidOperationException("se cayo la base");

        (await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.GetAll(true)))
            .Message.Should().Be("se cayo la base");
    }

    // ------------------------------------------------------------------ GetAll(campo, valor)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_rechaza_un_campo_de_busqueda_vacio(string? campo)
    {
        var (servicio, repo) = CrearCon(Venta(1, 100));

        (await Assert.ThrowsAsync<ArgumentException>(() => servicio.GetAll(campo!, "valor")))
            .Message.Should().Contain("Campo de búsqueda no válido.");
        repo.VecesListar.Should().Be(0);
    }

    [Fact]
    public async Task GetAll_rechaza_un_campo_no_soportado()
    {
        var (servicio, _) = CrearCon(Venta(1, 100));

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetAll("total", "10"));

        excepcion.Message.Should().Be(
            "Campo de búsqueda 'total' no soportado. Campos soportados: numero, nombre(Cliente), otro(Detalle).");
    }

    /// <summary>
    /// Caracteriza una inconsistencia del switch: <c>Producto</c> y <c>Cliente</c>
    /// aceptan "numero" y "numerodesc", pero <c>Venta</c> solo acepta "numero" (no hay
    /// case "numerodesc"). Igual con "otro"/"otrodesc": esos si existen, pero "numero"
    /// se quedo sin su variante descendente.
    /// </summary>
    [Fact]
    public async Task GetAll_no_soporta_numerodesc_a_diferencia_de_los_otros_dos_servicios()
    {
        var (servicio, _) = CrearCon(Venta(1, 100));

        (await Assert.ThrowsAsync<DataException>(() => servicio.GetAll("numerodesc", null)))
            .Message.Should().Contain("no soportado");

        // "otrodesc" si existe en Venta.
        (await servicio.GetAll("otrodesc", null)).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_por_numero_ordena_por_el_Numero_ascendente()
    {
        var (servicio, _) = CrearCon(Venta(1, 300), Venta(2, 100), Venta(3, 200));

        (await servicio.GetAll("numero", null)).Select(v => v.Numero).Should().Equal(100, 200, 300);
    }

    [Fact]
    public async Task GetAll_por_numero_filtra_con_coincidencia_parcial()
    {
        var (servicio, _) = CrearCon(Venta(1, 100), Venta(2, 1234), Venta(3, 555));

        (await servicio.GetAll("numero", "12")).Select(v => v.Numero).Should().Equal(1234);
    }

    [Fact]
    public async Task GetAll_ignora_las_ventas_sin_Numero()
    {
        var (servicio, _) = CrearCon(
            VentaBuilder.Uno().ConId(1).ConNumero(null).ConClientePorDefecto().Build(),
            Venta(2, 100));

        (await servicio.GetAll("numero", "1")).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_por_nombre_ordena_por_el_nombre_del_cliente()
    {
        var (servicio, _) = CrearCon(
            Venta(1, 100, cliente: Cliente("Zeta", 40111222)),
            Venta(2, 200, cliente: Cliente("Ana", 30111222)));

        (await servicio.GetAll("nombre", null))
            .Select(v => v.Cliente!.Nombre).Should().Equal("Ana", "Zeta");
    }

    [Fact]
    public async Task GetAll_por_nombredesc_ordena_por_el_nombre_del_cliente_descendente()
    {
        var (servicio, _) = CrearCon(
            Venta(1, 100, cliente: Cliente("Ana", 30111222)),
            Venta(2, 200, cliente: Cliente("Zeta", 40111222)));

        (await servicio.GetAll("nombredesc", null))
            .Select(v => v.Cliente!.Nombre).Should().Equal("Zeta", "Ana");
    }

    [Fact]
    public async Task GetAll_por_nombre_filtra_por_el_nombre_del_cliente()
    {
        var (servicio, _) = CrearCon(
            Venta(1, 100, cliente: Cliente("Ana Gomez", 30111222)),
            Venta(2, 200, cliente: Cliente("Beto Diaz", 33111222)));

        (await servicio.GetAll("nombre", "beto")).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_por_nombre_tolera_un_cliente_sin_nombre()
    {
        var (servicio, _) = CrearCon(
            VentaBuilder.Uno().ConId(1).ConNumero(100).ConCliente(ClienteBuilder.Uno().ConId(1).ConNombre("").Build()).Build(),
            Venta(2, 200, cliente: Cliente("Ana", 30111222)));

        (await servicio.GetAll("nombre", "ana")).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_por_otro_ordena_por_el_Detalle()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, detalle: "Zeta"), Venta(2, 200, detalle: "Ana"));

        (await servicio.GetAll("otro", null)).Select(v => v.Detalle).Should().Equal("Ana", "Zeta");
    }

    [Fact]
    public async Task GetAll_por_otrodesc_ordena_por_el_Detalle_descendente()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, detalle: "Ana"), Venta(2, 200, detalle: "Zeta"));

        (await servicio.GetAll("otrodesc", null)).Select(v => v.Detalle).Should().Equal("Zeta", "Ana");
    }

    [Fact]
    public async Task GetAll_por_otro_filtra_por_el_Detalle()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, detalle: "Compra mayorista"), Venta(2, 200, detalle: "Compra minorista"));

        (await servicio.GetAll("otro", "mayorista")).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_ignora_las_ventas_sin_Detalle()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, detalle: null), Venta(2, 200, detalle: "Compra"));

        (await servicio.GetAll("otro", "compra")).Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_sin_valor_no_arma_filtro_pero_si_ordena(string? valor)
    {
        var (servicio, repo) = CrearCon(Venta(1, 100), Venta(2, 200));

        (await servicio.GetAll("numero", valor)).Should().HaveCount(2);
        repo.UltimoFiltro.Should().BeNull();
    }

    [Fact]
    public async Task GetAll_sin_coincidencias_devuelve_vacio()
    {
        var (servicio, _) = CrearCon(Venta(1, 100));

        (await servicio.GetAll("nombre", "inexistente")).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ GetById

    [Fact]
    public async Task GetById_devuelve_la_venta_por_su_pk_interna()
    {
        var (servicio, _) = CrearCon(Venta(1, 100), Venta(2, 200));

        var resultado = await servicio.GetById(2);

        resultado.Numero.Should().Be(200);
    }

    [Fact]
    public async Task GetById_carga_las_tres_navegaciones()
    {
        var (servicio, repo) = CrearCon(Venta(1, 100));

        await servicio.GetById(1);

        repo.UltimoInclude.Should().Be("IdClienteNavigation.Venta,Ventaporproducto,Ventaporproducto.IdProductoNavigation");
    }

    [Fact]
    public async Task GetById_proyecta_cliente_items_y_total()
    {
        var (servicio, _) = CrearCon(Venta(1, 100, items: [Item(2, 1000m, "Gaseosa")]));

        var venta = await servicio.GetById(1);

        venta.Cliente!.Documento.Should().Be(30111222);
        venta.Productos!.Single().SubTotal.Should().Be(2000m);
        venta.Total.Should().Be(2000m);
    }

    /// <summary>
    /// Caracteriza que el mensaje de error dice "Numero de venta no válido" cuando lo
    /// que se valida es la <b>pk interna</b>. El cliente manda el numero de venta
    /// visible (100) y recibe el mismo error que si mandara un cero.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetById_rechaza_un_id_no_positivo_diciendo_Numero_de_venta(int id)
    {
        var (servicio, repo) = CrearCon(Venta(1, 100));

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetById(id));

        excepcion.Message.Should().Be("Numero de venta no válido");
        repo.VecesGetAsync.Should().Be(0);
    }

    /// <summary>
    /// Caracteriza que un id inexistente devuelve un <c>VentaDetalleDto</c> vacio en
    /// vez de <c>null</c> o una excepcion. Mismo patron que Producto y Cliente.
    /// Referencia: bug #35.
    /// </summary>
    [Fact]
    public async Task GetById_inexistente_devuelve_un_DTO_vacio_en_vez_de_null()
    {
        var (servicio, _) = CrearCon(Venta(1, 100));

        var resultado = await servicio.GetById(999);

        resultado.Should().NotBeNull();
        resultado.Numero.Should().BeNull();
        resultado.Detalle.Should().BeNull();
        resultado.Cliente.Should().BeNull();
        resultado.Productos.Should().BeNull();
        resultado.Total.Should().Be(0m);
    }

    /// <summary>
    /// Caracteriza que <c>VentaDetalleDto</c> no tiene campo <c>Id</c>: ni la pk
    /// interna ni el <c>Numero</c> vuelven cuando no esta. Un cliente que pide una
    /// venta inexistente no tiene forma de distinguir el "404" del "200 con cuerpo
    /// vacio", y no puede usar la respuesta para encadenar un <c>Update</c> (que si
    /// exige <c>Id</c>).
    /// Referencia: bug #35.
    /// </summary>
    [Fact]
    public async Task GetById_no_devuelve_la_pk_interna_que_exige_el_Update()
    {
        var (servicio, _) = CrearCon(Venta(1, 100));

        var resultado = await servicio.GetById(1);

        typeof(NicoPasino.Core.DTO.Ventas.VentaDetalleDto).GetProperties()
            .Select(p => p.Name).Should().NotContain("Id");
        resultado.Should().NotBeNull();
    }

    private static Cliente Cliente(string nombre, int documento) => ClienteBuilder.Uno()
        .ConId(documento == 30111222 ? 1 : 2)
        .ConDocumento(documento)
        .ConNombre(nombre)
        .ConCorreo("cliente@correo.com")
        .Build();
}