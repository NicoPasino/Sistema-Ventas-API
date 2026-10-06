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
/// Tests de <c>ProductoServicio.Update</c> (#17). El metodo tiene una regla
/// importante: si el DTO es identico al registro original devuelve <c>true</c>
/// <b>sin tocar la base</b> (lineas 159-168).
/// </summary>
public class ProductoServicioUpdateTests
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

    /// <summary>DTO identico al original, con el IdPublica que el metodo exige.</summary>
    private static ProductoDto MismoQueElOriginal() => ProductoDtoBuilder.Uno()
        .ConIdPublica(1001)
        .ConIdCategoria(1)
        .ConNombre("Gaseosa 500ml")
        .ConDescripcion("Botella de plástico")
        .ConPrecio(1500.50m)
        .ConCantidad(50)
        .ConStockMinimo(5)
        .ConStockMaximo(100)
        .ConProveedor("Distribuidora Central")
        .Build();

    // ------------------------------------------------------------------ camino feliz

    [Fact]
    public async Task Update_persiste_el_nuevo_nombre()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("Gaseosa 600ml").Build());

        repo.Entidades.Single().Nombre.Should().Be("Gaseosa 600ml");
    }

    [Fact]
    public async Task Update_devuelve_true()
    {
        var (servicio, _, _) = CrearCon();

        (await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("Nuevo").Build()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Update_busca_el_original_por_IdPublica()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("Nuevo").Build());

        repo.VecesGetAsync.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Update_conserva_la_pk_interna_del_registro_original()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("Nuevo").Build());

        repo.UltimaEntidadActualizada!.Id.Should().Be(50, "no se crea una entidad nueva");
        repo.Entidades.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_conserva_la_fecha_de_creacion_original()
    {
        var (servicio, repo, _) = CrearCon();
        var original = repo.Entidades.Single();
        original.FechaCreacion = new DateTime(2020, 5, 5, 0, 0, 0, DateTimeKind.Utc);

        await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("Nuevo").Build());

        repo.UltimaEntidadActualizada!.FechaCreacion.Should()
            .Be(new DateTime(2020, 5, 5, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Update_renueva_la_fecha_de_modificacion()
    {
        var (servicio, repo, _) = CrearCon();
        var antes = DateTime.UtcNow;

        await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("Nuevo").Build());

        repo.UltimaEntidadActualizada!.FechaModificacion.Should().NotBeNull();
        repo.UltimaEntidadActualizada.FechaModificacion!.Value.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Update_deja_la_navegacion_de_categoria_en_null()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConIdCategoria(1).ConNombre("Nuevo").Build());

        repo.UltimaEntidadActualizada!.IdCategoriaNavigation.Should().BeNull("linea 175");
    }

    [Fact]
    public async Task Update_persiste_el_cambio_de_categoria()
    {
        var (servicio, repo, _) = CrearCon();

        await servicio.Update(ProductoDtoBuilder.Uno().ConIdPublica(1001).ConIdCategoria(2).ConNombre("Nuevo").Build());

        repo.UltimaEntidadActualizada!.IdCategoria.Should().Be(2);
    }

    // ------------------------------------------------------------------ sin cambios

    [Fact]
    public async Task Update_sin_cambios_devuelve_true_sin_tocar_la_base()
    {
        var (servicio, repo, _) = CrearCon();

        var resultado = await servicio.Update(MismoQueElOriginal());

        resultado.Should().BeTrue();
        repo.VecesUpdate.Should().Be(0, "linea 167: sale temprano sin escribir");
    }

    [Fact]
    public async Task Update_solo_con_el_nombre_distinto_considera_que_hay_cambios()
    {
        var (servicio, repo, _) = CrearCon();
        var dto = MismoQueElOriginal();
        dto.Nombre = "Otro nombre";

        await servicio.Update(dto);

        repo.VecesUpdate.Should().Be(1);
    }

    [Fact]
    public async Task Update_solo_con_el_stock_minimo_distinto_considera_que_hay_cambios()
    {
        var (servicio, repo, _) = CrearCon();
        var dto = MismoQueElOriginal();
        dto.StockMinimo = 7;

        await servicio.Update(dto);

        repo.VecesUpdate.Should().Be(1);
    }

    // ------------------------------------------------------------------ validaciones

    [Fact]
    public async Task Update_rechaza_un_DTO_null()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(() => servicio.Update(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Update_rechaza_un_IdPublica_invalido(int? idPublica)
    {
        var (servicio, _, _) = CrearCon();
        var dto = ProductoDtoBuilder.Uno().ConIdPublica(idPublica).ConNombre("Nuevo").Build();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(dto));

        excepcion.Message.Should().Be("No se recibió un ID válido.");
    }

    [Fact]
    public async Task Update_rechaza_un_DTO_invalido()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(() => servicio.Update(
            ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("X").Build()));
    }

    [Fact]
    public async Task Update_rechaza_una_categoria_inexistente()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(() => servicio.Update(
            ProductoDtoBuilder.Uno().ConIdPublica(1001).ConIdCategoria(999).ConNombre("Nuevo").Build()));
    }

    [Fact]
    public async Task Update_falla_si_no_encuentra_el_producto_original()
    {
        var (servicio, _, _) = CrearCon([Original()]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(
            ProductoDtoBuilder.Uno().ConIdPublica(7777).ConNombre("Nuevo").Build()));

        excepcion.Message.Should().Be("Objeto original no encontrado.");
    }

    [Fact]
    public async Task Update_lanza_UpdateException_si_la_base_no_afecta_ninguna_fila()
    {
        var (servicio, repo, _) = CrearCon();
        repo.ResultadoUpdate = 0;

        var excepcion = await Assert.ThrowsAsync<UpdateException>(() => servicio.Update(
            ProductoDtoBuilder.Uno().ConIdPublica(1001).ConNombre("Nuevo").Build()));

        excepcion.Message.Should().Be("No pudo actualizar en la base de datos.");
    }
}