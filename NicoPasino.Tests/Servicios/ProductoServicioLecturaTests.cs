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
/// Tests de lectura de <see cref="ProductoServicio"/>: <c>GetAll(bool)</c> (#14) y
/// <c>GetById</c>. Los dos metodos de GetAll comparten la misma armazon, asi que
/// <c>GetAll(campo, valor)</c> se cubre en <see cref="ProductoServicioBusquedaTests"/>.
/// </summary>
public class ProductoServicioLecturaTests
{
    private static Categoria Bebidas => CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build();

    private static Producto Producto(int id, int idPublica, string nombre, DateTime? fechaModificacion = null,
        bool activo = true, string? proveedor = "Proveedor Norte")
        => ProductoBuilder.Uno()
            .ConId(id)
            .ConIdPublica(idPublica)
            .ConNombre(nombre)
            .ConFechaModificacion(fechaModificacion)
            .ConActivo(activo)
            .ConProveedor(proveedor)
            .ConCategoria(Bebidas)
            .Build();

    private static (ProductoServicio Servicio, RepositorioGenericoVentasFake<Producto> Repo,
        RepositorioGenericoVentasFake<Categoria> RepoCategoria)
        CrearCon(Producto[] productos = null!)
    {
        var repo = new RepositorioGenericoVentasFake<Producto>(productos ?? []);
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(Bebidas);
        return (new ProductoServicio(repo, new ProductoValidador(repoCategoria)), repo, repoCategoria);
    }

    // ------------------------------------------------------------------ GetAll(bool)

    [Fact]
    public async Task GetAll_devuelve_los_productos_ordenados_por_FechaModificacion_descendente()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "Antiguo", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Producto(2, 1002, "Reciente", new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            Producto(3, 1003, "Medio", new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc))
        ]);

        var resultado = await servicio.GetAll(true);

        resultado.Select(p => p.Nombre).Should().Equal("Reciente", "Medio", "Antiguo");
    }

    [Fact]
    public async Task GetAll_pide_la_categoria_navegada_para_poder_mapearla()
    {
        var (servicio, repo, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        await servicio.GetAll(true);

        repo.UltimoInclude.Should().Be("IdCategoriaNavigation");
    }

    [Fact]
    public async Task GetAll_mapea_el_nombre_de_la_categoria_al_DTO()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        var resultado = await servicio.GetAll(true);

        resultado.Single().Categoria.Should().Be("Bebidas");
    }

    [Fact]
    public async Task GetAll_devuelve_el_total_de_los_productos()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "A"), Producto(2, 1002, "B")]);

        (await servicio.GetAll(true)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_sin_productos_devuelve_una_coleccion_vacia_y_no_un_null()
    {
        var (servicio, _, _) = CrearCon();

        var resultado = await servicio.GetAll(true);

        resultado.Should().NotBeNull();
        resultado.Should().BeEmpty();
    }

    /// <summary>
    /// Caracteriza que <c>activo</c> <b>no se usa como filtro</b>: el filtro esta
    /// comentado en la linea 27 y la API filtra desde el front. Por eso un producto
    /// inactivo aparece igual en GetAll.
    /// Referencia: #14.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAll_ignora_el_parametro_activo_y_no_filtra(bool activo)
    {
        var (servicio, repo, _) = CrearCon([
            Producto(1, 1001, "Activo", activo: true),
            Producto(2, 1002, "Inactivo", activo: false)
        ]);

        var resultado = await servicio.GetAll(activo);

        resultado.Should().HaveCount(2);
        repo.UltimoFiltro.Should().BeNull("la linea 27 tiene el filtro comentado");
    }

    /// <summary>
    /// Caracteriza el anti-patron de la linea 39-40: cualquier excepcion del
    /// repositorio o del mapeo se re-lanza como <c>Exception</c> generico,
    /// perdiendo el tipo original y el stack trace.
    /// Referencia: bug #47.
    /// </summary>
    [Fact]
    public async Task GetAll_reenvuelve_cualquier_excepcion_como_Exception_generica()
    {
        var (servicio, repo, _) = CrearCon();
        repo.AlListarAsync = () => throw new InvalidOperationException("fallo de base de datos");

        var excepcion = await Assert.ThrowsAsync<Exception>(() => servicio.GetAll(true));

        excepcion.Should().BeOfType<Exception>();
        excepcion.Message.Should().Be("fallo de base de datos");
        excepcion.InnerException.Should().BeNull("el catch descarta la excepcion original");
    }

    /// <summary>
    /// Caracteriza que el <c>catch</c> tambien se come las <c>UpdateException</c> y las
    /// <c>DataException</c>, que nunca llegan al controller con su tipo.
    /// Referencia: bug #47.
    /// </summary>
    [Fact]
    public async Task GetAll_revierte_el_tipo_de_las_excepciones_de_negocio()
    {
        var (servicio, repo, _) = CrearCon();
        repo.AlListarAsync = () => throw new UpdateException("No pudo actualizar en la base de datos.");

        var excepcion = await Assert.ThrowsAsync<Exception>(() => servicio.GetAll(true));

        excepcion.Should().NotBeOfType<UpdateException>();
        excepcion.Message.Should().Be("No pudo actualizar en la base de datos.");
    }

    // ------------------------------------------------------------------ GetById

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetById_rechaza_un_id_no_positivo(int id)
    {
        var (servicio, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetById(id));

        excepcion.Message.Should().Be("Id no válido");
    }

    [Fact]
    public async Task GetById_busca_por_IdPublica_y_no_por_la_pk_interna()
    {
        var (servicio, _, _) = CrearCon([Producto(50, 1001, "Gaseosa")]);

        var resultado = await servicio.GetById(1001);

        resultado.Nombre.Should().Be("Gaseosa");
    }

    [Fact]
    public async Task GetById_no_encuentra_un_producto_por_su_pk_interna()
    {
        var (servicio, _, _) = CrearCon([Producto(50, 1001, "Gaseosa")]);

        var resultado = await servicio.GetById(50);

        resultado.Nombre.Should().BeNull("no existe producto con IdPublica == 50");
    }

    [Fact]
    public async Task GetById_pide_la_categoria_navegada()
    {
        var (servicio, repo, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        await servicio.GetById(1001);

        repo.UltimoInclude.Should().Be("IdCategoriaNavigation");
    }

    [Fact]
    public async Task GetById_mapea_el_nombre_de_la_categoria_al_DTO()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        (await servicio.GetById(1001)).Categoria.Should().Be("Bebidas");
    }

    [Fact]
    public async Task GetById_devuelve_los_datos_completos_del_producto()
    {
        var (servicio, _, _) = CrearCon([
            ProductoBuilder.Uno().ConId(1).ConIdPublica(1001).ConNombre("Gaseosa")
                .ConPrecio(1234.56m).ConCantidad(7).ConProveedor("Proveedor Norte").Build()
        ]);

        var resultado = await servicio.GetById(1001);

        resultado.Nombre.Should().Be("Gaseosa");
        resultado.Precio.Should().Be(1234.56m);
        resultado.Cantidad.Should().Be(7);
        resultado.Proveedor.Should().Be("Proveedor Norte");
        resultado.IdPublica.Should().Be(1001);
    }

    /// <summary>
    /// Caracteriza que "no encontrado" se comunica con un DTO vacio en vez de
    /// <c>null</c> o una excepcion. El controller no puede distinguirlo de un
    /// producto con todos los campos en cero.
    /// Referencia: bug #35.
    /// </summary>
    [Fact]
    public async Task GetById_devuelve_un_DTO_vacio_cuando_no_encuentra_el_producto()
    {
        var (servicio, _, _) = CrearCon();

        var resultado = await servicio.GetById(9999);

        resultado.Should().NotBeNull();
        resultado.Nombre.Should().BeNull();
        resultado.Precio.Should().Be(0);
        resultado.Activo.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_devuelve_productos_inactivos_sin_filtrar()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "Inactivo", activo: false)]);

        (await servicio.GetById(1001)).Activo.Should().BeFalse();
    }
}