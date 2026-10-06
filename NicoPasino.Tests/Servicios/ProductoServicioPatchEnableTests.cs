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
/// Tests de <c>ProductoServicio.Patch</c> (#17). A diferencia de <c>Update</c>, el
/// Patch es <b>parcial</b>: solo toca los campos que vienen y todos son
/// validables de a uno.
/// </summary>
public class ProductoServicioPatchTests
{
    private static Categoria Bebidas => CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build();
    private static Categoria Limpieza => CategoriaBuilder.Uno().ConId(2).ConNombre("Limpieza").Build();

    private static Producto Original() => ProductoBuilder.Uno()
        .ConId(50)
        .ConIdPublica(1001)
        .ConNombre("Gaseosa 500ml")
        .ConDescripcion("Botella de plástico")
        .ConPrecio(1500.50m)
        .ConCantidad(50)
        .ConStockMinimo(5)
        .ConStockMaximo(100)
        .ConProveedor("Distribuidora Central")
        .ConCategoria(Bebidas)
        .Build();

    private static (ProductoServicio Servicio, RepositorioGenericoVentasFake<Producto> Repo,
        RepositorioGenericoVentasFake<Categoria> RepoCategoria)
        CrearCon(Producto[] productos = null!)
    {
        var repo = new RepositorioGenericoVentasFake<Producto>(productos ?? [Original()]);
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(Bebidas, Limpieza);
        return (new ProductoServicio(repo, new ProductoValidador(repoCategoria)), repo, repoCategoria);
    }

    private static ProductoPatchDto Patch() => new() { Nombre = "Nuevo nombre" };

    // ------------------------------------------------------------------ validaciones de entrada

    [Fact]
    public async Task Patch_rechaza_un_DTO_null()
    {
        var (servicio, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Patch(null!, 1001));

        excepcion.Message.Should().Be("No se recibió ningún dato.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Patch_rechaza_un_IdPublica_no_positivo(int idPublica)
    {
        var (servicio, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Patch(Patch(), idPublica));

        excepcion.Message.Should().Be("No se recibió ningún ID.");
    }

    [Fact]
    public async Task Patch_rechaza_un_patch_sin_ningun_campo()
    {
        var (servicio, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Patch(new ProductoPatchDto(), 1001));

        excepcion.Message.Should().Be("No se recibieron datos para actualizar.");
    }

    [Fact]
    public async Task Patch_acepta_un_patch_solo_con_Activo()
    {
        // El chequeo de "patch vacio" (lineas 191-199) incluye Activo entre los
        // campos, asi que un patch con solo Activo=false si se considera con datos.
        var (servicio, repo, _) = CrearCon();

        (await servicio.Patch(new ProductoPatchDto { Activo = false }, 1001)).Should().BeTrue();
        repo.Entidades.Single().Activo.Should().BeFalse();
    }

    [Fact]
    public async Task Patch_falla_si_no_encuentra_el_producto()
    {
        var (servicio, _, _) = CrearCon([Original()]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Patch(Patch(), 7777));

        excepcion.Message.Should().Be("Producto no encontrado.");
    }

    // ------------------------------------------------------------------ campo por campo

    [Fact]
    public async Task Patch_actualiza_el_nombre_y_lo_recorta()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { Nombre = "  Nuevo nombre  " }, 1001);

        repo.Entidades.Single().Nombre.Should().Be("Nuevo nombre");
    }

    [Fact]
    public async Task Patch_rechaza_un_nombre_invalido()
    {
        var (servicio, repo, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { Nombre = "X" }, 1001));

        repo.VecesUpdate.Should().Be(0);
    }

    [Fact]
    public async Task Patch_actualiza_la_descripcion_y_la_pone_en_null_si_queda_vacia()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { Descripcion = "   " }, 1001);

        repo.Entidades.Single().Descripcion.Should().BeNull();
    }

    [Fact]
    public async Task Patch_actualiza_la_descripcion_si_viene_con_texto()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { Descripcion = "Nueva descripcion" }, 1001);

        repo.Entidades.Single().Descripcion.Should().Be("Nueva descripcion", "la rama del ternario");
    }

    [Fact]
    public async Task Patch_rechaza_una_descripcion_de_mas_de_500_caracteres()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { Descripcion = new string('a', 501) }, 1001));
    }

    [Fact]
    public async Task Patch_actualiza_la_cantidad()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { Cantidad = 12 }, 1001);

        repo.Entidades.Single().Cantidad.Should().Be(12);
    }

    [Fact]
    public async Task Patch_rechaza_una_cantidad_negativa()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { Cantidad = -1 }, 1001));
    }

    [Fact]
    public async Task Patch_actualiza_el_precio()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { Precio = 1999.99m }, 1001);

        repo.Entidades.Single().Precio.Should().Be(1999.99m);
    }

    [Fact]
    public async Task Patch_rechaza_un_precio_negativo()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { Precio = -1m }, 1001));
    }

    [Fact]
    public async Task Patch_rechaza_un_precio_que_supera_el_maximo()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { Precio = ProductoValidador.PrecioMaximo + 1 }, 1001));
    }

    [Fact]
    public async Task Patch_actualiza_el_stock_minimo_y_el_maximo()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { StockMinimo = 2, StockMaximo = 20 }, 1001);

        repo.Entidades.Single().StockMinimo.Should().Be(2);
        repo.Entidades.Single().StockMaximo.Should().Be(20);
    }

    /// <summary>
    /// Contracara de <c>ClienteServicio.Patch</c>, que con el mismo envio <b>no</b>
    /// puede borrar el telefono: aca no hay guard posterior, asi que el
    /// <c>string.IsNullOrEmpty</c> de la linea 236 si se aplica y el proveedor
    /// queda en null.
    /// </summary>
    [Fact]
    public async Task Patch_puede_borrar_el_proveedor_enviando_los_espacios()
    {
        var (servicio, repo, _) = CrearCon();

        (await servicio.Patch(new ProductoPatchDto { Proveedor = "   " }, 1001)).Should().BeTrue();
        repo.Entidades.Single().Proveedor.Should().BeNull();
    }

    [Fact]
    public async Task Patch_rechaza_un_proveedor_de_mas_de_150_caracteres()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { Proveedor = new string('a', 151) }, 1001));
    }

    [Fact]
    public async Task Patch_actualiza_el_proveedor_y_lo_recorta()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { Proveedor = "  Nuevo Proveedor  " }, 1001);

        repo.Entidades.Single().Proveedor.Should().Be("Nuevo Proveedor");
    }

    [Fact]
    public async Task Patch_actualiza_la_categoria_y_valida_que_exista()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { IdCategoria = 2 }, 1001);

        repo.Entidades.Single().IdCategoria.Should().Be(2);
        repo.Entidades.Single().IdCategoriaNavigation.Should().BeNull();
    }

    [Fact]
    public async Task Patch_rechaza_una_categoria_inexistente()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { IdCategoria = 999 }, 1001));
    }

    [Fact]
    public async Task Patch_rechaza_una_categoria_no_positiva()
    {
        var (servicio, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { IdCategoria = 0 }, 1001));

        excepcion.Message.Should().Be("La categoría no es válida.");
    }

    [Fact]
    public async Task Patch_aplica_varios_campos_a_la_vez()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto {
            Nombre = "Nuevo",
            Cantidad = 3,
            Precio = 99.90m,
            Activo = false
        }, 1001);

        var guardado = repo.Entidades.Single();
        guardado.Nombre.Should().Be("Nuevo");
        guardado.Cantidad.Should().Be(3);
        guardado.Precio.Should().Be(99.90m);
        guardado.Activo.Should().BeFalse();
    }

    // ------------------------------------------------------------------ chequeo cruzado de stock

    [Fact]
    public async Task Patch_rechaza_un_stock_minimo_mayor_al_maximo_persistido()
    {
        var (servicio, _, _) = CrearCon();
        // El original tiene StockMaximo = 100.
        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { StockMinimo = 200 }, 1001));

        excepcion.Message.Should().Be("El stock mínimo no puede ser mayor al stock máximo.");
    }

    [Fact]
    public async Task Patch_rechaza_un_stock_maximo_menor_al_minimo_persistido()
    {
        var (servicio, _, _) = CrearCon();
        // El original tiene StockMinimo = 5.
        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { StockMaximo = 2 }, 1001));

        excepcion.Message.Should().Be("El stock mínimo no puede ser mayor al stock máximo.");
    }

    [Fact]
    public async Task Patch_acepta_un_stock_minimo_nuevo_si_sigue_siendo_menor_al_maximo_persistido()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Patch(new ProductoPatchDto { StockMinimo = 99 }, 1001);

        repo.Entidades.Single().StockMinimo.Should().Be(99);
    }

    [Fact]
    public async Task Patch_rechaza_un_rango_invertido_dentro_del_mismo_patch()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ProductoPatchDto { StockMinimo = 90, StockMaximo = 10 }, 1001));
    }

    // ------------------------------------------------------------------ resultado

    [Fact]
    public async Task Patch_renueva_la_fecha_de_modificacion()
    {
        var (servicio, repo, _) = CrearCon();
        var antes = DateTime.UtcNow;

        await servicio.Patch(Patch(), 1001);

        repo.UltimaEntidadActualizada!.FechaModificacion!.Value.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Patch_devuelve_true()
    {
        var (servicio, _, _) = CrearCon();

        (await servicio.Patch(Patch(), 1001)).Should().BeTrue();
    }

    [Fact]
    public async Task Patch_lanza_UpdateException_si_la_base_no_afecta_ninguna_fila()
    {
        var (servicio, repo, _) = CrearCon();
        repo.ResultadoUpdate = 0;

        var excepcion = await Assert.ThrowsAsync<UpdateException>(() => servicio.Patch(Patch(), 1001));

        excepcion.Message.Should().Be("No pudo actualizar en la base de datos.");
    }
}

/// <summary>
/// Tests de <c>ProductoServicio.Enable</c> (#18). Ojo: el metodo <b>invierte</b> el
/// estado (<c>Activo = !Activo</c>) en vez de <b>asignar</b> el que recibe.
/// </summary>
public class ProductoServicioEnableTests
{
    private static Categoria Bebidas => CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build();

    private static Producto Original(bool activo = true) => ProductoBuilder.Uno()
        .ConId(50).ConIdPublica(1001).ConNombre("Gaseosa").ConActivo(activo).ConCategoria(Bebidas).Build();

    private static (ProductoServicio Servicio, RepositorioGenericoVentasFake<Producto> Repo)
        CrearCon(params Producto[] productos)
    {
        var repo = new RepositorioGenericoVentasFake<Producto>(productos.Length > 0 ? productos : [Original()]);
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(Bebidas);
        return (new ProductoServicio(repo, new ProductoValidador(repoCategoria)), repo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Enable_rechaza_un_id_no_positivo(int id)
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Enable(id, true));

        excepcion.Message.Should().Be("Id no válido");
    }

    [Fact]
    public async Task Enable_busca_por_el_IdPublica()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Enable(1001, false);

        repo.VecesGetAsync.Should().Be(1);
    }

    [Fact]
    public async Task Enable_falla_si_no_encuentra_el_producto()
    {
        var (servicio, _) = CrearCon([Original()]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Enable(7777, true));

        excepcion.Message.Should().Be("Id no válido");
    }

    /// <summary>
    /// Caracteriza que <c>Enable</c> <b>invierte</b> el estado en vez de asignarlo:
    /// la linea 268 es <c>Activo = !Activo</c>, asi que el parametro <c>estado</c> se
    /// ignora por completo. Un cliente que mande <c>estado = true</c> sobre un
    /// producto ya activo lo <b>desactiva</b>.
    /// Referencia: bug #40.
    /// </summary>
    [Fact]
    public async Task Enable_invierte_el_estado_en_vez_de_asignar_el_estado_recibido()
    {
        var (servicio, repo) = CrearCon(Original(activo: true));

        await servicio.Enable(1001, true);

        repo.Entidades.Single().Activo.Should().BeFalse("estado = true sobre un activo lo apaga");
    }

    [Fact]
    public async Task Enable_sobre_un_producto_inactivo_lo_enciende_aunque_se_pida_apagarlo()
    {
        var (servicio, repo) = CrearCon(Original(activo: false));

        await servicio.Enable(1001, false);

        repo.Entidades.Single().Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Enable_devuelve_true()
    {
        var (servicio, _) = CrearCon();

        (await servicio.Enable(1001, false)).Should().BeTrue();
    }

    [Fact]
    public async Task Enable_renueva_la_fecha_de_modificacion()
    {
        var (servicio, repo) = CrearCon();
        var antes = DateTime.UtcNow;

        await servicio.Enable(1001, false);

        repo.UltimaEntidadActualizada!.FechaModificacion!.Value.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Enable_persiste_el_cambio_del_estado()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Enable(1001, false);

        repo.VecesUpdate.Should().Be(1);
        repo.Entidades.Single().Activo.Should().BeFalse();
    }

    /// <summary>
    /// Contraste con <c>ClienteServicio.Enable</c>, que si asigna <c>Activo = estado</c>.
    /// Los dos metodos tienen la misma firma y semantica opuesta.
    /// Referencia: bug #40.
    /// </summary>
    [Fact]
    public async Task Enable_de_producto_y_de_cliente_tienen_semantica_opuesta()
    {
        var (servicioProducto, repoProducto) = CrearCon(Original(activo: true));
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>(
            ClienteBuilder.Uno().ConId(1).ConDocumento(30111222).ConActivo(true).Build());
        var servicioCliente = new ClienteServicio(repoCliente, new ClienteValidador(repoCliente));

        await servicioProducto.Enable(1001, true);
        await servicioCliente.Enable(30111222, true);

        repoProducto.Entidades.Single().Activo.Should().BeFalse();
        repoCliente.Entidades.Single().Activo.Should().BeTrue();
    }
}