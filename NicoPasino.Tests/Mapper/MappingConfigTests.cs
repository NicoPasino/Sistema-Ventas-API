using FluentAssertions;
using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Mapper;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Tests.Common.Builders;

namespace NicoPasino.Tests.Mapper;

/// <summary>
/// Tests de los 5 mapeos de <see cref="MappingConfig.VentasMappings"/> (#25).
/// <para>
/// Estos mapeos son los que hacen que existan <c>ProductoDto.Categoria</c>,
/// <c>VentaDetalleDto.Total</c> o <c>ClienteDto.NroCompras</c>: si uno esta mal, los
/// tests de servicio de arriba que mapean via <c>Adapt</c> dan falsos positivos.
/// </para>
/// <para>
/// Los 3 <c>NullReferenceException</c> documentados son a proposito: los mapeos
/// desreferencian navegaciones sin validar, asi que si el <c>Include</c> no se aplico
/// el <c>Adapt</c> revienta. Es la razon por la que los tests de servicio asertan el
/// string de Include.
/// </para>
/// </summary>
public class MappingConfigTests
{
    // ============================================================= Producto -> ProductoDto

    [Fact]
    public void Producto_mapea_el_nombre_de_la_categoria_desde_la_navegacion()
    {
        var categoria = CategoriaBuilder.Uno().ConId(3).ConNombre("Bebidas").Build();
        var producto = ProductoBuilder.Uno().ConCategoria(categoria).Build();

        var dto = producto.Adapt<ProductoDto>();

        dto.Categoria.Should().Be("Bebidas");
        dto.IdCategoria.Should().Be(3);
    }

    [Fact]
    public void Producto_mapea_los_campos_basicos()
    {
        var producto = ProductoBuilder.Uno()
            .ConNombre("Gaseosa 500ml")
            .ConPrecio(1500.50m)
            .ConCantidad(50)
            .ConActivo(true)
            .Build();

        var dto = producto.Adapt<ProductoDto>();

        dto.Nombre.Should().Be("Gaseosa 500ml");
        dto.Precio.Should().Be(1500.50m);
        dto.Cantidad.Should().Be(50);
        dto.Activo.Should().BeTrue();
        dto.IdPublica.Should().Be(1001);
    }

    [Fact]
    public void Producto_dto_a_producto_y_vuelta_conserva_los_campos()
    {
        var dto = ProductoDtoBuilder.Uno()
            .ConNombre("Gaseosa 500ml")
            .ConPrecio(1200m)
            .ConCantidad(30)
            .ConIdCategoria(2)
            .Build();

        var producto = dto.Adapt<Producto>();
        var ida = producto.Adapt<ProductoDto>();

        producto.Nombre.Should().Be(dto.Nombre);
        producto.Precio.Should().Be(dto.Precio);
        producto.Cantidad.Should().Be(dto.Cantidad);
        producto.IdCategoria.Should().Be(dto.IdCategoria);
        ida.Nombre.Should().Be(dto.Nombre);
        ida.Precio.Should().Be(dto.Precio);
        ida.Cantidad.Should().Be(dto.Cantidad);
        ida.IdCategoria.Should().Be(dto.IdCategoria);
    }

    /// <summary>
    /// <c>MappingConfig</c> linea 12 hace <c>src.IdCategoriaNavigation.Nombre</c>. Al
    /// no estar la navegacion cargada (sin <c>Include</c>) Mapster es null-safe y la
    /// propiedad calculada <c>Categoria</c> queda en <c>null</c> en vez de reventar:
    /// el error pasa silencioso al DTO.
    /// </summary>
    [Fact]
    public void Producto_sin_IdCategoriaNavigation_mapea_Categoria_null()
    {
        var producto = ProductoBuilder.Uno().ConIdCategoriaHuerfano(9).Build();

        var dto = producto.Adapt<ProductoDto>();

        dto.Categoria.Should().BeNull();
        dto.IdCategoria.Should().Be(9, "el FK si se conserva");
    }

    // ================================================================= Venta -> VentaDto

    [Fact]
    public void VentaDto_mapea_el_cliente_como_string_del_nombre()
    {
        var venta = VentaBuilder.Uno().ConClientePorDefecto().Build();
        venta.IdClienteNavigation.Nombre = "Juan Perez";

        var dto = venta.Adapt<VentaDto>();

        dto.Cliente.Should().Be("Juan Perez");
        dto.Numero.Should().Be(1);
    }

    [Fact]
    public void VentaDto_mapea_los_productos_desde_Ventaporproducto()
    {
        var item = VentaporproductoBuilder.Uno()
            .ConPrecioUnitario(100m, "Gaseosa 500ml")
            .ConCantidad(2)
            .Build();
        var venta = VentaBuilder.Uno().ConClientePorDefecto().ConItems(item).Build();

        var dto = venta.Adapt<VentaDto>();

        dto.Productos.Should().ContainSingle();
        dto.Productos!.Single().Producto.Should().Be("Gaseosa 500ml");
        dto.Productos!.Single().Cantidad.Should().Be(2);
    }

    // ======================================================== Venta -> VentaDetalleDto

    [Fact]
    public void VentaDetalleDto_total_con_un_solo_item()
    {
        var item = VentaporproductoBuilder.Uno().ConPrecioUnitario(10m).ConCantidad(2).Build();
        var venta = VentaBuilder.Uno().ConClientePorDefecto().ConItems(item).Build();

        var dto = venta.Adapt<VentaDetalleDto>();

        dto.Total.Should().Be(20m, "10 x 2 del unico item");
    }

    [Fact]
    public void VentaDetalleDto_total_con_varios_items()
    {
        var venta = VentaBuilder.Uno().ConClientePorDefecto().ConItems(
            VentaporproductoBuilder.Uno().ConPrecioUnitario(10m).ConCantidad(2).Build(),
            VentaporproductoBuilder.Uno().ConPrecioUnitario(15m).ConCantidad(3).Build(),
            VentaporproductoBuilder.Uno().ConPrecioUnitario(5m).ConCantidad(1).Build()
        ).Build();

        var dto = venta.Adapt<VentaDetalleDto>();

        dto.Total.Should().Be(70m, "20 + 45 + 5");
        dto.Productos.Should().HaveCount(3);
    }

    [Fact]
    public void VentaDetalleDto_total_cero_con_la_lista_vacia()
    {
        var venta = VentaBuilder.Uno().ConClientePorDefecto().Build();

        var dto = venta.Adapt<VentaDetalleDto>();

        dto.Total.Should().Be(0m);
        dto.Productos.Should().BeEmpty();
    }

    [Fact]
    public void VentaDetalleDto_cliente_es_el_DTO_completo()
    {
        var cliente = ClienteBuilder.Uno().ConId(1).ConNombre("Ana Gomez").ConDocumento(30111222)
            .ConCorreo("ana@correo.com").ConCompras(
                VentaBuilder.Uno().ConId(1).Build(),
                VentaBuilder.Uno().ConId(2).Build())
            .Build();
        var venta = VentaBuilder.Uno().ConCliente(cliente).Build();

        var dto = venta.Adapt<VentaDetalleDto>();

        dto.Cliente.Should().NotBeNull();
        dto.Cliente!.Nombre.Should().Be("Ana Gomez");
        dto.Cliente!.Documento.Should().Be(30111222);
        dto.Cliente!.Correo.Should().Be("ana@correo.com");
        dto.Cliente!.NroCompras.Should().Be(2, "contraste con VentaDto donde es solo un string");
    }

    /// <summary>
    /// El <c>Total</c> es <c>src.Ventaporproducto.Sum(...)</c>. Con la coleccion
    /// <c>null</c> (sin <c>Include</c>) la <c>Sum</c> de LINQ lanza
    /// <c>ArgumentNullException</c> ("Value cannot be null"); el mapeo no la convierte
    /// ni la escala a 0. Es el riesgo real del Include faltante en VentaDetalleDto
    /// (a diferencia de la navegacion de Producto, que Mapster si deja en null).
    /// </summary>
    [Fact]
    public void VentaDetalleDto_con_Ventaporproducto_null_lanza_ArgumentNullException()
    {
        var venta = VentaBuilder.Uno().ConClientePorDefecto().Build();
        venta.Ventaporproducto = null!;

        var excepcion = Assert.Throws<ArgumentNullException>(() => venta.Adapt<VentaDetalleDto>());

        excepcion.ParamName.Should().Be("source");
    }

    [Fact]
    public void VentaDetalleDto_mapea_los_productos_y_sus_subtotales()
    {
        var venta = VentaBuilder.Uno().ConClientePorDefecto().ConItems(
            VentaporproductoBuilder.Uno().ConPrecioUnitario(10m).ConCantidad(2).Build()).Build();

        var dto = venta.Adapt<VentaDetalleDto>();

        var item = dto.Productos!.Single();
        item.PrecioUnitario.Should().Be(10m);
        item.Cantidad.Should().Be(2);
        item.SubTotal.Should().Be(20m);
    }

    // ============================================== Ventaporproducto -> VentaporproductoDto

    [Fact]
    public void VentaporproductoDto_gana_el_snapshot_NombreProducto()
    {
        var producto = ProductoBuilder.Uno().ConId(7).ConNombre("Gaseosa 500ml").Build();
        var item = VentaporproductoBuilder.Uno().ConProducto(producto)
            .ConPrecioUnitario(100m, "Snapshot viejo").Build();

        var dto = item.Adapt<VentaporproductoDto>();

        dto.Producto.Should().Be("Snapshot viejo", "el snapshot gana aunque la navegacion este");
    }

    [Fact]
    public void VentaporproductoDto_sin_snapshot_cae_al_nombre_de_la_navegacion()
    {
        var producto = ProductoBuilder.Uno().ConId(7).ConNombre("Gaseosa 500ml").Build();
        var item = VentaporproductoBuilder.Uno().ConProducto(producto).Build();
        item.NombreProducto = null!;

        var dto = item.Adapt<VentaporproductoDto>();

        dto.Producto.Should().Be("Gaseosa 500ml", "fallback a IdProductoNavigation.Nombre");
    }

    /// <summary>
    /// Riesgo 3: <c>src.NombreProducto ?? src.IdProductoNavigation!.Nombre</c> con
    /// ambas nulas -> NullReferenceException.
    /// </summary>
    [Fact]
    public void VentaporproductoDto_sin_snapshot_ni_navegacion_lanza_NullReferenceException()
    {
        var item = VentaporproductoBuilder.Uno().Build();
        item.NombreProducto = null!;
        item.IdProductoNavigation = null!;

        Assert.Throws<NullReferenceException>(() => item.Adapt<VentaporproductoDto>());
    }

    [Fact]
    public void VentaporproductoDto_mapea_cantidad_precio_y_subtotal()
    {
        var item = VentaporproductoBuilder.Uno().ConPrecioUnitario(25.50m).ConCantidad(4).Build();

        var dto = item.Adapt<VentaporproductoDto>();

        dto.Cantidad.Should().Be(4);
        dto.PrecioUnitario.Should().Be(25.50m);
        dto.SubTotal.Should().Be(102m);
    }

    // ============================================================== Cliente -> ClienteDto

    [Fact]
    public void ClienteDto_mapea_los_campos_basicos()
    {
        var cliente = ClienteBuilder.Uno().ConNombre("Ana Gomez").ConDocumento(30111222).Build();

        var dto = cliente.Adapt<ClienteDto>();

        dto.Nombre.Should().Be("Ana Gomez");
        dto.Documento.Should().Be(30111222);
        dto.Activo.Should().BeTrue();
    }

    [Fact]
    public void ClienteDto_nro_compras_cero_con_ventas_vacias()
    {
        var cliente = ClienteBuilder.Uno().Build();

        (cliente.Adapt<ClienteDto>()).NroCompras.Should().Be(0);
    }

    [Fact]
    public void ClienteDto_nro_compras_con_una_venta()
    {
        var cliente = ClienteBuilder.Uno().ConCompras(VentaBuilder.Uno().ConId(1).Build()).Build();

        (cliente.Adapt<ClienteDto>()).NroCompras.Should().Be(1);
    }

    [Fact]
    public void ClienteDto_nro_compras_con_varias_ventas()
    {
        var cliente = ClienteBuilder.Uno().ConCompras(
            VentaBuilder.Uno().ConId(1).Build(),
            VentaBuilder.Uno().ConId(2).Build(),
            VentaBuilder.Uno().ConId(3).Build()).Build();

        (cliente.Adapt<ClienteDto>()).NroCompras.Should().Be(3);
    }
}