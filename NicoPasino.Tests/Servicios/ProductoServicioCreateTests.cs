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
/// Tests de <c>ProductoServicio.Create</c> (#16), con foco en la generacion del
/// <c>IdPublica</c>: el metodo sortea un numero al azar y lo verifica contra el
/// repositorio hasta 10 veces.
/// </summary>
public class ProductoServicioCreateTests
{
    private static Categoria Bebidas => CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build();

    private static (ProductoServicio Servicio, RepositorioGenericoVentasFake<Producto> Repo,
        RepositorioGenericoVentasFake<Categoria> RepoCategoria)
        CrearCon(Producto[] productos = null!)
    {
        var repo = new RepositorioGenericoVentasFake<Producto>(productos ?? []);
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(Bebidas);
        return (new ProductoServicio(repo, new ProductoValidador(repoCategoria)), repo, repoCategoria);
    }

    [Fact]
    public async Task Create_agrega_el_producto_y_devuelve_true()
    {
        var (servicio, repo, _) = CrearCon();
        var dto = ProductoDtoBuilder.Uno().Build();

        var resultado = await servicio.Create(dto);

        resultado.Should().BeTrue();
        repo.VecesAdd.Should().Be(1);
        repo.Entidades.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_persiste_el_nombre_el_precio_y_el_stock()
    {
        var (servicio, repo, _) = CrearCon();
        var dto = ProductoDtoBuilder.Uno()
            .ConNombre("Gaseosa 500ml")
            .ConPrecio(1500.50m)
            .ConCantidad(50)
            .Build();

        await servicio.Create(dto);

        var guardado = repo.Entidades.Single();
        guardado.Nombre.Should().Be("Gaseosa 500ml");
        guardado.Precio.Should().Be(1500.50m);
        guardado.Cantidad.Should().Be(50);
    }

    [Fact]
    public async Task Create_marca_el_producto_como_activo()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Create(ProductoDtoBuilder.Uno().Build());

        repo.Entidades.Single().Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Create_asigna_las_dos_fechas_al_momento_actual()
    {
        var (servicio, repo, _) = CrearCon();
        var antes = DateTime.UtcNow;

        await servicio.Create(ProductoDtoBuilder.Uno().Build());

        var guardado = repo.Entidades.Single();
        guardado.FechaCreacion.Should().NotBeNull();
        guardado.FechaModificacion.Should().NotBeNull();
        guardado.FechaCreacion!.Value.Should().BeOnOrAfter(antes);
        guardado.FechaModificacion!.Value.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Create_ignora_la_fecha_de_creacion_que_venue_en_el_DTO()
    {
        var (servicio, repo, _) = CrearCon();
        var dto = ProductoDtoBuilder.Uno().Build();
        dto.FechaCreacion = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await servicio.Create(dto);

        repo.Entidades.Single().FechaCreacion!.Value.Should().BeAfter(new DateTime(2001, 1, 1));
    }

    [Fact]
    public async Task Create_asigna_un_IdPublica_dentro_del_rango_sorteado()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Create(ProductoDtoBuilder.Uno().ConIdPublica(null).Build());

        // Random.Shared.Next(1, 9999999) excluye el extremo superior.
        repo.UltimaEntidadAgregada!.IdPublica.Should().BeInRange(1, 9999998);
    }

    [Fact]
    public async Task Create_sobrescribe_el_IdPublica_que_venia_en_el_DTO()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Create(ProductoDtoBuilder.Uno().ConIdPublica(4242).Build());

        repo.UltimaEntidadAgregada!.IdPublica.Should().NotBe(4242);
    }

    [Fact]
    public async Task Create_deja_la_navegacion_de_categoria_en_null_para_no_insertar_una_categoria_nueva()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Create(ProductoDtoBuilder.Uno().ConIdCategoria(1).Build());

        var guardado = repo.UltimaEntidadAgregada!;
        guardado.IdCategoria.Should().Be(1, "solo se usa la FK");
        guardado.IdCategoriaNavigation.Should().BeNull("linea 142");
    }

    [Fact]
    public async Task Create_rechaza_una_categoria_inexistente()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(() => servicio.Create(ProductoDtoBuilder.Uno().ConIdCategoria(999).Build()));
    }

    [Fact]
    public async Task Create_rechaza_un_DTO_invalido_sin_agregar_nada()
    {
        var (servicio, repo, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Create(ProductoDtoBuilder.Uno().ConNombre("X").Build()));

        repo.VecesAdd.Should().Be(0);
    }

    [Fact]
    public async Task Create_devuelve_false_si_el_repositorio_no_devuelve_la_entidad()
    {
        var (servicio, repo, _) = CrearCon();
        repo.AlAdd = _ => null;

        (await servicio.Create(ProductoDtoBuilder.Uno().Build())).Should().BeFalse();
    }

    // ------------------------------------------------------------------ GenerarIdPublicaUnicoAsync

    [Fact]
    public async Task Create_verifica_el_IdPublica_sorteado_contra_el_repositorio()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Create(ProductoDtoBuilder.Uno().Build());

        repo.VecesGetAsync.Should().Be(1);
        repo.UltimoFiltro.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_reintenta_hasta_encontrar_un_IdPublica_libre()
    {
        var (servicio, repo, _) = CrearCon();

        // Los dos primeros intentos chocan, el tercero pasa.
        var intentos = 0;
        repo.AlGetAsync = _ => ++intentos < 3
            ? ProductoBuilder.Uno().ConId(1).ConIdPublica(1).Build()
            : null;

        (await servicio.Create(ProductoDtoBuilder.Uno().Build())).Should().BeTrue();
        repo.VecesGetAsync.Should().Be(3);
    }

    [Fact]
    public async Task Create_falla_si_despues_de_los_diez_intentos_no_encuentra_un_codigo_libre()
    {
        var (servicio, repo, _) = CrearCon();

        // El unico producto existente tiene IdPublica == 1, asi que el filtro del fake
        // devuelve null siempre: no es un choque real, solo sirve para agotar los
        // 10 intentos del bucle.
        repo.AlGetAsync = _ => ProductoBuilder.Uno().ConId(1).ConIdPublica(1).Build();

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Create(ProductoDtoBuilder.Uno().Build()));

        excepcion.Message.Should().Be("No se pudo generar un código único para el producto, reintentar.");
        repo.VecesGetAsync.Should().Be(10, "IntentosIdPublica = 10");
    }

    [Fact]
    public async Task Create_no_agrega_el_producto_si_no_puede_generar_el_codigo()
    {
        var (servicio, repo, _) = CrearCon();
        repo.AlGetAsync = _ => ProductoBuilder.Uno().ConId(1).ConIdPublica(1).Build();

        await Assert.ThrowsAsync<DataException>(() => servicio.Create(ProductoDtoBuilder.Uno().Build()));

        repo.VecesAdd.Should().Be(0, "el IdPublica se genera antes del Add");
    }

    /// <summary>
    /// Contraste con <c>GetAll</c>: aca <b>no</b> hay <c>catch (Exception)</c>, asi que
    /// una falla del repositorio conserva su tipo y su stack trace. Un POST que
    /// devuelve 500 por caida de la base no llega al wrap de
    /// <c>Exception(ex.Message)</c>.
    /// Referencia: bug #47.
    /// </summary>
    [Fact]
    public async Task Create_propaga_la_excepcion_del_repositorio_sin_envolverla()
    {
        var (servicio, repo, _) = CrearCon();
        repo.AlGetAsync = _ => throw new InvalidOperationException("se cayo la base");

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.Create(ProductoDtoBuilder.Uno().Build()));

        excepcion.Message.Should().Be("se cayo la base");
    }
}