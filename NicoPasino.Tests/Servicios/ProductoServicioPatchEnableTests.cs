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
/// Tests de <c>ProductoServicio.Enable</c> (#18, #34). La API ya no permite baja
/// fisica: habilitar/deshabilitar se hace con PATCH, asi que <c>Enable</c> queda
/// como contrato de <c>IServicioGenerico</c> y lanza <c>NotImplementedException</c>.
/// </summary>
public class ProductoServicioEnableTests
{
    private static (ProductoServicio Servicio, RepositorioGenericoVentasFake<Producto> Repo) CrearCon()
    {
        var repo = new RepositorioGenericoVentasFake<Producto>(
            [ProductoBuilder.Uno().ConId(50).ConIdPublica(1001).Build()]);
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(
            [CategoriaBuilder.Uno().ConId(1).Build()]);
        return (new ProductoServicio(repo, new ProductoValidador(repoCategoria)), repo);
    }

    /// <summary>
    /// Caracteriza que la baja logica de productos ya no esta implementada: el
    /// endpoint DELETE se elimino y la habilitacion/deshabilitacion se hace por
    /// PATCH. La interfaz <c>IServicioGenerico</c> obliga a declarar el metodo.
    /// </summary>
    [Fact]
    public async Task Enable_lanza_NotImplementedException_siempre()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(1001, true));

        excepcion.Message.Should().Be(
            "La baja de productos no está implementada: usar PATCH con 'activo' para habilitarlo o deshabilitarlo.");
    }

    [Fact]
    public async Task Enable_falla_igual_para_el_estado_true_que_para_el_false()
    {
        var (servicio, _) = CrearCon();

        await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(1001, false));
    }

    [Fact]
    public async Task Enable_no_toca_la_base_ni_valida_el_id()
    {
        var (servicio, repo) = CrearCon();

        await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(0, true));

        repo.VecesGetAsync.Should().Be(0);
        repo.VecesUpdate.Should().Be(0);
    }
}