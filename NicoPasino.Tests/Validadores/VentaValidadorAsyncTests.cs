using FluentAssertions;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Validadores;

/// <summary>
/// Tests de los validadores asincronos de <see cref="VentaValidador"/> (#13):
/// <c>ValidarClienteAsync</c> y <c>ValidarProductosAsync</c>. Son los que
/// consultar el repositorio y los que concentran las reglas de negocio de una
/// venta: el cliente tiene que existir, el producto tiene que estar activo y hay stock.
/// </summary>
public class VentaValidadorAsyncTests
{
    private static Producto ProductoValido(int idPublica, string nombre, int stock = 50)
        => ProductoBuilder.Uno().ConIdPublica(idPublica).ConNombre(nombre).ConCantidad(stock).Build();

    private static Cliente ClienteValido(int id, int documento)
        => ClienteBuilder.Uno().ConId(id).ConDocumento(documento).Build();

    private static (VentaValidador Validador, RepositorioGenericoVentasFake<Cliente> RepoCliente,
        RepositorioGenericoVentasFake<Producto> RepoProducto)
        CrearCon(Cliente[]? clientes = null, Producto[]? productos = null)
    {
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>(clientes ?? []);
        var repoProducto = new RepositorioGenericoVentasFake<Producto>(productos ?? []);
        return (new VentaValidador(repoCliente, repoProducto), repoCliente, repoProducto);
    }

    // ------------------------------------------------------- ValidarClienteAsync

    [Fact]
    public async Task ValidarClienteAsync_devuelve_el_Id_del_cliente_encontrado()
    {
        var (validador, _, _) = CrearCon(clientes: [ClienteValido(7, 30111222)]);

        var id = await validador.ValidarClienteAsync(30111222);

        id.Should().Be(7);
    }

    [Fact]
    public async Task ValidarClienteAsync_rechaza_un_DNI_que_no_corresponde_a_ningun_cliente()
    {
        var (validador, _, _) = CrearCon(clientes: [ClienteValido(7, 30111222)]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarClienteAsync(40998877));

        excepcion.Message.Should().Be("El cliente con el DNI '40998877' no existe.");
    }

    [Fact]
    public async Task ValidarClienteAsync_rechaza_un_DNI_null_sin_consultar_el_repo()
    {
        var (validador, repoCliente, _) = CrearCon(clientes: [ClienteValido(7, 30111222)]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarClienteAsync(null));

        repoCliente.VecesGetAsync.Should().Be(0, "ValidaDni corta antes de consultar");
    }

    [Fact]
    public async Task ValidarClienteAsync_rechaza_un_DNI_con_menos_de_8_digitos()
    {
        var (validador, _, _) = CrearCon(clientes: [ClienteValido(7, 30111222)]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarClienteAsync(123));
        excepcion.Message.Should().Be("El DNI debe tener 8 dígitos.");
    }

    [Fact]
    public async Task ValidarClienteAsync_consulta_por_Documento_y_no_por_Id()
    {
        var (validador, repoCliente, _) = CrearCon(clientes: [ClienteValido(7, 30111222)]);

        await validador.ValidarClienteAsync(30111222);

        repoCliente.VecesGetAsync.Should().Be(1);
        repoCliente.UltimoFiltro.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidarClienteAsync_no_falla_por_un_cliente_inactivo()
    {
        // El validador no mira Activo: la venta se permite sobre clientes inactivos.
        var (validador, _, _) = CrearCon(clientes: [ClienteBuilder.Uno().ConId(7).ConDocumento(30111222).ConActivo(false).Build()]);

        (await validador.ValidarClienteAsync(30111222)).Should().Be(7);
    }

    // ------------------------------------------------------- ValidarProductosAsync

    [Fact]
    public async Task ValidarProductosAsync_devuelve_los_productos_en_el_mismo_orden_que_se_pidieron()
    {
        var (validador, _, _) = CrearCon(productos: [
            ProductoValido(1001, "Gaseosa"),
            ProductoValido(1002, "Agua")
        ]);

        var productos = await validador.ValidarProductosAsync([1002, 1001], [1, 1]);

        productos.Select(p => p.Nombre).Should().Equal("Agua", "Gaseosa");
    }

    [Fact]
    public async Task ValidarProductosAsync_acepta_una_venta_con_un_solo_producto()
    {
        var (validador, _, _) = CrearCon(productos: [ProductoValido(1001, "Gaseosa")]);

        (await validador.ValidarProductosAsync([1001], [3])).Should().HaveCount(1);
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_un_producto_inexistente()
    {
        var (validador, _, _) = CrearCon(productos: [ProductoValido(1001, "Gaseosa")]);

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => validador.ValidarProductosAsync([9999], [1])
        );

        excepcion.Message.Should().Be("Producto no encontrado (código del producto: 9999).");
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_un_producto_inactivo()
    {
        var (validador, _, _) = CrearCon(productos: [
            ProductoBuilder.Uno().ConIdPublica(1001).ConNombre("Gaseosa").ConActivo(false).Build()
        ]);

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => validador.ValidarProductosAsync([1001], [1])
        );

        excepcion.Message.Should().Be("El producto 'Gaseosa' está inactivo y no se puede vender.");
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_si_la_cantidad_supera_el_stock()
    {
        var (validador, _, _) = CrearCon(productos: [ProductoValido(1001, "Gaseosa", stock: 5)]);

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => validador.ValidarProductosAsync([1001], [10])
        );

        excepcion.Message.Should().Be(
            "Stock insuficiente para 'Gaseosa', cantidad solicitada: '10', disponible: '5'.");
    }

    [Fact]
    public async Task ValidarProductosAsync_acepta_una_cantidad_igual_al_stock_disponible()
    {
        var (validador, _, _) = CrearCon(productos: [ProductoValido(1001, "Gaseosa", stock: 5)]);

        (await validador.ValidarProductosAsync([1001], [5])).Should().HaveCount(1);
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_una_lista_de_ids_null()
    {
        var (validador, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync(null!, [1]));
        excepcion.Message.Should().Be("No se recibió la lista de productos.");
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_una_lista_de_cantidades_null()
    {
        var (validador, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([1001], null!));
        excepcion.Message.Should().Be("No se recibió la lista de cantidades.");
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_listas_de_distinto_largo()
    {
        var (validador, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([1001, 1002], [1]));
        excepcion.Message.Should().Be("La lista de productos no coincide con la lista de cantidades.");
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_las_dos_listas_vacias()
    {
        var (validador, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([], []));
        excepcion.Message.Should().Be("Debe haber al menos un producto en la venta.");
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_un_codigo_de_producto_no_positivo()
    {
        var (validador, _, repoProducto) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([0], [1]));
        excepcion.Message.Should().Be("El código del producto no es válido.");

        repoProducto.VecesGetAsync.Should().Be(0, "valida el id antes de consultar");
    }

    [Fact]
    public async Task ValidarProductosAsync_rechaza_una_cantidad_de_cero()
    {
        var (validador, _, _) = CrearCon(productos: [ProductoValido(1001, "Gaseosa")]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([1001], [0]));
        excepcion.Message.Should().Be("La cantidad debe ser mayor a cero.");
    }

    [Fact]
    public async Task ValidarProductosAsync_valida_en_orden_y_aborta_en_el_primer_producto_invalido()
    {
        var (validador, _, repoProducto) = CrearCon(productos: [ProductoValido(1001, "Gaseosa")]);

        // El primer producto existe y esta activo; el segundo no existe.
        await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([1001, 9999], [1, 1]));

        repoProducto.VecesGetAsync.Should().Be(2, "valida secuencialmente y corta en el segundo");
    }

    [Fact]
    public async Task ValidarProductosAsync_acepta_una_venta_con_varios_productos_validos()
    {
        var (validador, _, _) = CrearCon(productos: [
            ProductoValido(1001, "Gaseosa"),
            ProductoValido(1002, "Agua"),
            ProductoValido(1003, "Jugo", stock: 3)
        ]);

        var productos = await validador.ValidarProductosAsync([1001, 1002, 1003], [1, 2, 3]);

        productos.Should().HaveCount(3);
    }

    [Fact]
    public async Task ValidarProductosAsync_acepta_el_mismo_producto_repetido()
    {
        var (validador, _, _) = CrearCon(productos: [ProductoValido(1001, "Gaseosa", stock: 10)]);

        var productos = await validador.ValidarProductosAsync([1001, 1001], [2, 3]);

        productos.Should().HaveCount(2, "no valida duplicados de item, solo los valida contra el stock");
    }

    [Fact]
    public async Task ValidarProductosAsync_busca_por_IdPublica_y_no_por_la_pk_interna()
    {
        var (validador, _, repoProducto) = CrearCon(productos: [
            ProductoBuilder.Uno().ConId(50).ConIdPublica(1001).ConNombre("Gaseosa").Build()
        ]);

        await validador.ValidarProductosAsync([1001], [1]);

        repoProducto.VecesGetAsync.Should().Be(1);
        repoProducto.UltimoFiltro.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidarProductosAsync_no_encuentra_un_producto_por_su_pk_interna()
    {
        var (validador, _, _) = CrearCon(productos: [
            ProductoBuilder.Uno().ConId(50).ConIdPublica(1001).ConNombre("Gaseosa").Build()
        ]);

        await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([50], [1]));
    }

    [Fact]
    public async Task ValidarProductosAsync_no_acepta_un_producto_con_IdPublica_null()
    {
        var (validador, _, _) = CrearCon(productos: [
            ProductoBuilder.Uno().ConId(50).ConIdPublica(null).ConNombre("Gaseosa").Build()
        ]);

        await Assert.ThrowsAsync<DataException>(() => validador.ValidarProductosAsync([1001], [1]));
    }

    /// <summary>
    /// Los productos que devuelve el validador son <b>copias</b> (el fake replica el
    /// <c>AsNoTracking()</c> de <c>GetAsync</c>). Eso importa porque
    /// <c>VentaServicio.Create</c> descuenta el stock sobre estos objetos y despues
    /// llama a <c>Update</c>: si fueran las instancias trackeadas el stock ya estaria
    /// modificado en memoria y un <c>Update</c> olvidado pasaria desapercibido.
    /// Referencia: #13.
    /// </summary>
    [Fact]
    public async Task ValidarProductosAsync_devuelve_copias_y_no_las_instancias_almacenadas()
    {
        var productoEnRepo = ProductoValido(1001, "Gaseosa", stock: 10);
        var (validador, _, _) = CrearCon(productos: [productoEnRepo]);

        var productos = await validador.ValidarProductosAsync([1001], [2]);

        var devuelto = productos.Single();
        devuelto.Should().NotBeSameAs(productoEnRepo);
        devuelto.Cantidad.Should().Be(10);

        devuelto.Cantidad = 8;
        productoEnRepo.Cantidad.Should().Be(10, "mutar la copia no toca el almacen");
    }
}