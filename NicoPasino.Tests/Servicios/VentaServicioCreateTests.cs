using FluentAssertions;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de <c>VentaServicio.Create</c> (#22). El metodo hace <b>cuatro</b>
/// escrituras sin transaccion (hay un <c>// TODO: usar uow?</c> en la linea 13):
/// guarda la venta, guarda un <c>Ventaporproducto</c> por item y descuenta el stock
/// producto por producto.
/// </summary>
public class VentaServicioCreateTests
{
    private static readonly DateTime FechaFija = new(2024, 3, 10, 15, 30, 0, DateTimeKind.Utc);

    private static Cliente ClienteValido => ClienteBuilder.Uno()
        .ConId(1).ConDocumento(30111222).ConNombre("Ana Gomez").ConCorreo("ana@correo.com").Build();

    private static Producto Producto(int id, int idPublica, string nombre = "Gaseosa 500ml",
        decimal precio = 1500.50m, int cantidad = 50, bool activo = true)
        => ProductoBuilder.Uno()
            .ConId(id)
            .ConIdPublica(idPublica)
            .ConNombre(nombre)
            .ConPrecio(precio)
            .ConCantidad(cantidad)
            .ConActivo(activo)
            .ConCategoria(CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build())
            .Build();

    private static VentaDto DtoValido(int[]? ids = null, int[]? cants = null, int dni = 30111222) => new() {
        DNI = dni,
        Detalle = "Compra mayorista",
        FechaVenta = FechaFija,
        ItemsId = ids ?? [1001, 1002],
        ItemsCant = cants ?? [3, 2]
    };

    private static (VentaServicio Servicio, RepositorioGenericoVentasFake<Venta> RepoVenta,
        RepositorioGenericoVentasFake<Cliente> RepoCliente, RepositorioGenericoVentasFake<Producto> RepoProducto,
        RepositorioGenericoVentasFake<Ventaporproducto> RepoVpp)
        CrearCon(Producto[] productos = null!, Cliente[] clientes = null!)
    {
        var repoVenta = new RepositorioGenericoVentasFake<Venta>();
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>(clientes ?? [ClienteValido]);
        var repoProducto = new RepositorioGenericoVentasFake<Producto>(productos ?? [
            Producto(1, 1001, cantidad: 50),
            Producto(2, 1002, "Agua Mineral", 900m, cantidad: 20)
        ]);
        var repoVpp = new RepositorioGenericoVentasFake<Ventaporproducto>();
        var servicio = new VentaServicio(repoVenta, repoCliente, repoProducto, repoVpp,
            new VentaValidador(repoCliente, repoProducto));
        return (servicio, repoVenta, repoCliente, repoProducto, repoVpp);
    }

    // ------------------------------------------------------------------ camino feliz

    [Fact]
    public async Task Create_guarda_la_venta_y_devuelve_true()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();

        (await servicio.Create(DtoValido())).Should().BeTrue();
        repoVenta.VecesAdd.Should().Be(1);
        repoVenta.Entidades.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_resuelve_el_IdCliente_a_partir_del_DNI()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();

        await servicio.Create(DtoValido());

        repoVenta.UltimaEntidadAgregada!.IdCliente.Should().Be(1, "linea 124: el Id del cliente, no el DNI");
        repoVenta.UltimaEntidadAgregada.IdCliente.Should().NotBe(30111222);
    }

    [Fact]
    public async Task Create_persiste_el_detalle_y_la_fecha_del_DTO()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();

        await servicio.Create(DtoValido());

        repoVenta.UltimaEntidadAgregada!.Detalle.Should().Be("Compra mayorista");
        repoVenta.UltimaEntidadAgregada.FechaVenta.Should().Be(FechaFija);
    }

    [Fact]
    public async Task Create_usa_la_fecha_actual_si_el_DTO_no_trae_fecha()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();
        var antes = DateTime.UtcNow;
        var dto = DtoValido();
        dto.FechaVenta = null;

        await servicio.Create(dto);

        repoVenta.UltimaEntidadAgregada!.FechaVenta.Should().NotBeNull();
        repoVenta.UltimaEntidadAgregada.FechaVenta!.Value.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Create_ignora_el_Detalle_vacio()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();
        var dto = DtoValido();
        dto.Detalle = "   ";

        await servicio.Create(dto);

        repoVenta.UltimaEntidadAgregada!.Detalle.Should().BeNull();
    }

    [Fact]
    public async Task Create_sortea_un_Numero_dentro_del_rango()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();

        await servicio.Create(DtoValido());

        // Random.Next(1, 9999999) excluye el extremo superior.
        repoVenta.UltimaEntidadAgregada!.Numero.Should().BeInRange(1, 9999998);
    }

    /// <summary>
    /// Caracteriza que el <c>Numero</c> que viene en el DTO se descarta: se genera
    /// uno al azar. Un cliente que POSTea una venta con numero propio no lo ve
    /// respetado.
    /// </summary>
    [Fact]
    public async Task Create_sobrescribe_el_Numero_que_venia_en_el_DTO()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();
        var dto = DtoValido();
        dto.Numero = 4242;

        await servicio.Create(dto);

        repoVenta.UltimaEntidadAgregada!.Numero.Should().NotBe(4242);
    }

    // ------------------------------------------------------------------ items

    [Fact]
    public async Task Create_guarda_un_Ventaporproducto_por_item()
    {
        var (servicio, _, _, _, repoVpp) = CrearCon();

        await servicio.Create(DtoValido());

        repoVpp.VecesAdd.Should().Be(2);
        repoVpp.Entidades.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_conecta_el_item_con_la_pk_de_la_venta_guardada()
    {
        var (servicio, repoVenta, _, _, repoVpp) = CrearCon();

        await servicio.Create(DtoValido([1001], [3]));

        repoVpp.Entidades.Single().IdVenta.Should().Be(repoVenta.UltimaEntidadAgregada!.Id);
    }

    [Fact]
    public async Task Create_guarda_el_Id_interno_del_producto_y_no_el_IdPublica()
    {
        var (servicio, _, _, _, repoVpp) = CrearCon();

        await servicio.Create(DtoValido([1001], [3]));

        repoVpp.Entidades.Single().IdProducto.Should().Be(1, "el IdPublica 1001 es lo que manda el cliente");
    }

    [Fact]
    public async Task Create_hace_un_snapshot_del_nombre_y_del_precio_del_producto()
    {
        var (servicio, _, _, _, repoVpp) = CrearCon();

        await servicio.Create(DtoValido([1002], [2]));

        var item = repoVpp.Entidades.Single();
        item.NombreProducto.Should().Be("Agua Mineral");
        item.PrecioUnitario.Should().Be(900m);
        item.SubTotal.Should().Be(1800m);
    }

    [Fact]
    public async Task Create_conserva_la_cantidad_pedida_en_el_item()
    {
        var (servicio, _, _, _, repoVpp) = CrearCon();

        await servicio.Create(DtoValido([1001, 1002], [3, 2]));

        repoVpp.Entidades.Select(i => i.Cantidad).Should().BeEquivalentTo([3, 2]);
    }

    // ------------------------------------------------------------------ stock

    [Fact]
    public async Task Create_descuenta_el_stock_de_cada_producto()
    {
        var (servicio, _, _, repoProducto, _) = CrearCon();

        await servicio.Create(DtoValido([1001, 1002], [3, 2]));

        repoProducto.Entidades.Single(p => p.Id == 1).Cantidad.Should().Be(47);
        repoProducto.Entidades.Single(p => p.Id == 2).Cantidad.Should().Be(18);
    }

    [Fact]
    public async Task Create_persiste_el_stock_descontado()
    {
        var (servicio, _, _, repoProducto, _) = CrearCon();

        await servicio.Create(DtoValido([1001], [5]));

        repoProducto.VecesUpdate.Should().Be(1);
        repoProducto.UltimaEntidadActualizada!.Cantidad.Should().Be(45);
    }

    [Fact]
    public async Task Create_puede_vender_todo_el_stock_disponible()
    {
        var (servicio, _, _, repoProducto, _) = CrearCon([Producto(1, 1001, cantidad: 5)]);

        await servicio.Create(DtoValido([1001], [5]));

        repoProducto.Entidades.Single().Cantidad.Should().Be(0, "deja en cero, no en negativo");
    }

    // ------------------------------------------------------------------ validaciones

    [Fact]
    public async Task Create_rechaza_un_DTO_null()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(null!));

        excepcion.Message.Should().Be("No se recibió ningún dato.");
        repoVenta.VecesAdd.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1234567)]
    public async Task Create_rechaza_un_DNI_invalido(int? dni)
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();
        var dto = DtoValido();
        dto.DNI = dni;

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(dto));

        excepcion.Message.Should().Be(dni == null ? "No se recibió el DNI del cliente." : "El DNI debe tener 8 dígitos.");
        repoVenta.VecesAdd.Should().Be(0);
    }

    [Fact]
    public async Task Create_rechaza_un_cliente_inexistente()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido(dni: 33111222)));

        excepcion.Message.Should().Be("El cliente con el DNI '33111222' no existe.");
        repoVenta.VecesAdd.Should().Be(0);
    }

    /// <summary>
    /// Caracteriza que <c>Create</c> no valida que el cliente este activo, solo que
    /// exista. Contraste con <c>ValidarProductosAsync</c>, que si rechaza los productos
    /// inactivos. Un cliente dado de baja todavia puede comprar.
    /// </summary>
    [Fact]
    public async Task Create_no_valida_que_el_cliente_este_activo()
    {
        var clienteInactivo = ClienteBuilder.Uno()
            .ConId(1).ConDocumento(30111222).ConNombre("Ana Gomez").ConCorreo("ana@correo.com").ConActivo(false).Build();
        var (servicio, _, _, _, _) = CrearCon(clientes: [clienteInactivo]);

        (await servicio.Create(DtoValido())).Should().BeTrue();
    }

    [Fact]
    public async Task Create_rechaza_un_detalle_de_mas_de_500_caracteres()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();
        var dto = DtoValido();
        dto.Detalle = new string('a', 501);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(dto));

        excepcion.Message.Should().Be("El detalle no puede tener más de 500 caracteres.");
        repoVenta.VecesAdd.Should().Be(0);
    }

    [Fact]
    public async Task Create_rechaza_una_fecha_futura()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();
        var dto = DtoValido();
        dto.FechaVenta = DateTime.UtcNow.AddDays(1);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(dto));

        excepcion.Message.Should().Be("La fecha de la venta no puede ser futura.");
        repoVenta.VecesAdd.Should().Be(0);
    }

    [Fact]
    public async Task Create_rechaza_una_lista_de_productos_vacia()
    {
        var (servicio, repoVenta, _, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido([], [])));

        excepcion.Message.Should().Be("Debe haber al menos un producto en la venta.");
        repoVenta.VecesAdd.Should().Be(0);
    }

    [Fact]
    public async Task Create_rechaza_listas_de_distinta_longitud()
    {
        var (servicio, _, _, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Create(DtoValido([1001, 1002], [3])));

        excepcion.Message.Should().Be("La lista de productos no coincide con la lista de cantidades.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Create_rechaza_un_codigo_de_producto_no_positivo(int id)
    {
        var (servicio, _, _, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido([id], [1])));

        excepcion.Message.Should().Be("El código del producto no es válido.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_rechaza_una_cantidad_no_positiva(int cantidad)
    {
        var (servicio, _, _, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido([1001], [cantidad])));

        excepcion.Message.Should().Be("La cantidad debe ser mayor a cero.");
    }

    [Fact]
    public async Task Create_rechaza_un_producto_inexistente()
    {
        var (servicio, _, _, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido([7777], [1])));

        excepcion.Message.Should().Be("Producto no encontrado (código del producto: 7777).");
    }

    [Fact]
    public async Task Create_rechaza_un_producto_inactivo()
    {
        var (servicio, _, _, _, _) = CrearCon([Producto(1, 1001, nombre: "Gaseosa Retirada", activo: false)]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido([1001], [1])));

        excepcion.Message.Should().Be("El producto 'Gaseosa Retirada' está inactivo y no se puede vender.");
    }

    [Fact]
    public async Task Create_rechaza_una_cantidad_superior_al_stock()
    {
        var (servicio, _, _, _, _) = CrearCon([Producto(1, 1001, cantidad: 10)]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido([1001], [11])));

        excepcion.Message.Should().Be(
            "Stock insuficiente para 'Gaseosa 500ml', cantidad solicitada: '11', disponible: '10'.");
    }

    [Fact]
    public async Task Create_no_agrega_nada_si_alguno_de_los_productos_falla_la_validacion()
    {
        var (servicio, repoVenta, _, _, repoVpp) = CrearCon([Producto(1, 1001), Producto(2, 1002, cantidad: 0)]);

        await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido([1001, 1002], [1, 1])));

        repoVenta.VecesAdd.Should().Be(0, "valida todo antes de escribir");
        repoVpp.VecesAdd.Should().Be(0);
    }

    // ------------------------------------------------------------------ fallos de escritura

    [Fact]
    public async Task Create_lanza_UpdateException_si_no_se_puede_guardar_la_venta()
    {
        var (servicio, repoVenta, _, _, repoVpp) = CrearCon();
        repoVenta.AlAdd = _ => null;

        var excepcion = await Assert.ThrowsAsync<UpdateException>(() => servicio.Create(DtoValido()));

        excepcion.Message.Should().Be("Error al guardar la venta.");
        repoVpp.VecesAdd.Should().Be(0, "corta antes de los items");
    }

    /// <summary>
    /// Caracteriza la falta de transaccion (el <c>// TODO: usar uow?</c> de la linea
    /// 13). Si el descuento de stock del <b>segundo</b> producto falla, ya quedaron
    /// guardados la venta y los dos items, y el stock del <b>primer</b> producto ya
    /// fue descontado. Queda una venta persistida a la que le falta parte del stock:
    /// un rollback de verdad lo evitaria.
    /// </summary>
    [Fact]
    public async Task Create_deja_una_venta_a_medias_si_el_stock_de_un_producto_no_se_actualiza()
    {
        var (servicio, repoVenta, _, repoProducto, repoVpp) = CrearCon();
        repoProducto.ResultadosUpdate.Enqueue(1);
        repoProducto.ResultadosUpdate.Enqueue(0);

        var excepcion = await Assert.ThrowsAsync<UpdateException>(() => servicio.Create(DtoValido()));

        excepcion.Message.Should().Be("No se pudo actualizar el stock del producto.");
        repoVenta.VecesAdd.Should().Be(1, "la venta ya estaba guardada");
        repoVpp.VecesAdd.Should().Be(2, "los items ya estaban guardados");
        repoProducto.Entidades.Single(p => p.Id == 1).Cantidad.Should().Be(47, "el primer descuento si se persistio");
        repoProducto.Entidades.Single(p => p.Id == 2).Cantidad.Should().Be(20, "el segundo no");
    }

    [Fact]
    public async Task Create_no_deja_stock_negativo_si_la_validacion_pasa()
    {
        var (servicio, _, _, repoProducto, _) = CrearCon([Producto(1, 1001, cantidad: 2)]);

        await servicio.Create(DtoValido([1001], [2]));

        repoProducto.Entidades.Single().Cantidad.Should().Be(0);
    }
}