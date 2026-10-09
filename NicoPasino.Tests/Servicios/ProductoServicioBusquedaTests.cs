using FluentAssertions;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de <c>ProductoServicio.GetAll(campo, valor)</c> (#15): los 4 campos de
/// busqueda, sus variants "desc" y el contraste entre buscar con valor y sin valor.
/// </summary>
public class ProductoServicioBusquedaTests
{
    private static Categoria Bebidas => CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build();
    private static Categoria Comidas => CategoriaBuilder.Uno().ConId(2).ConNombre("Comidas").Build();

    private static Producto Producto(int id, int idPublica, string nombre, Categoria categoria = null!,
        string? proveedor = "Proveedor Norte", bool activo = true)
        => ProductoBuilder.Uno()
            .ConId(id)
            .ConIdPublica(idPublica)
            .ConNombre(nombre)
            .ConCategoria(categoria ?? Bebidas)
            .ConProveedor(proveedor)
            .ConActivo(activo)
            .Build();

    private static (ProductoServicio Servicio, RepositorioGenericoVentasFake<Producto> Repo,
        RepositorioGenericoVentasFake<Categoria> RepoCategoria)
        CrearCon(params Producto[] productos)
    {
        var repo = new RepositorioGenericoVentasFake<Producto>(productos);
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(Bebidas, Comidas);
        return (new ProductoServicio(repo, new ProductoValidador(repoCategoria)), repo, repoCategoria);
    }

    // ------------------------------------------------------------------ validacion del campo

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_rechaza_un_campo_de_busqueda_vacio(string? campo)
    {
        var (servicio, repo, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetAll(campo!, "valor"));

        excepcion.Message.Should().Contain("Campo de búsqueda no válido.");
        repo.VecesListar.Should().Be(0, "falla antes de tocar el repositorio");
    }

    [Fact]
    public async Task GetAll_rechaza_un_campo_no_soportado()
    {
        var (servicio, _, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetAll("precio", "10"));

        excepcion.Message.Should().Be(
            "Campo de búsqueda 'precio' no soportado. Campos soportados: numero, nombre, otro(Nombre Categoría), proveedor(Nombre Proveedor).");
    }

    [Fact]
    public async Task GetAll_normaliza_el_campo_con_Trim_y_ToLowerInvariant()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        await servicio.GetAll("  NOMBRE  ", "gaseosa");

        // Si el switch no normalizara, el campo caeria en el default.
        (await servicio.GetAll("  NOMBRE  ", "gaseosa")).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAll_normaliza_el_valor_con_Trim_y_lo_compara_en_minusculas()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        var resultado = await servicio.GetAll("nombre", "  GASEOsa  ");

        resultado.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_pide_la_categoria_navegada()
    {
        var (servicio, repo, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        await servicio.GetAll("nombre", "gaseosa");

        repo.UltimoInclude.Should().Be("IdCategoriaNavigation");
    }

    // ------------------------------------------------------------------ sin valor: solo orden

    /// <summary>
    /// Caracteriza que sin valor el filtro queda en <c>null</c>, asi que <b>tambien
    /// entran los productos inactivos</b>. Con valor, en cambio, todos los filtros
    /// empiezan por <c>m.Activo</c> y los inactivos desaparecen. Es una
    /// inconsistencia: la misma busqueda con y sin valor devuelve conjuntos distintos.
    /// Referencia: #15.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_sin_valor_no_filtra_e_incluye_los_inactivos(string? valor)
    {
        var (servicio, repo, _) = CrearCon([
            Producto(1, 1001, "Gaseosa", activo: true),
            Producto(2, 1002, "Gaseosa Retirada", activo: false)
        ]);

        var resultado = await servicio.GetAll("nombre", valor);

        resultado.Should().HaveCount(2);
        repo.UltimoFiltro.Should().BeNull("sin valor no se arma filtro");
    }

    /// <summary>
    /// Contracara directa del test anterior: con valor el mismo filtro excluye
    /// los inactivos.
    /// </summary>
    [Fact]
    public async Task GetAll_con_valor_excluye_los_inactivos_por_el_filtro_Activo()
    {
        var (servicio, repo, _) = CrearCon([
            Producto(1, 1001, "Gaseosa", activo: true),
            Producto(2, 1002, "Gaseosa Retirada", activo: false)
        ]);

        var resultado = await servicio.GetAll("nombre", "gaseosa");

        resultado.Should().ContainSingle();
        repo.UltimoFiltro.Should().NotBeNull();
    }

    // ------------------------------------------------------------------ campo "numero"

    [Fact]
    public async Task GetAll_por_numero_ordena_por_la_pk_interna_ascendente()
    {
        var (servicio, repo, _) = CrearCon([
            Producto(3, 1003, "C"),
            Producto(1, 1001, "A"),
            Producto(2, 1002, "B")
        ]);

        await servicio.GetAll("numero", null);

        // ProductoDto no expone la pk interna, asi que el orden se verifica sobre
        // el delegate que el servicio le paso al repositorio.
        repo.OrdenEsperado().Select(p => p.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task GetAll_por_numero_filtra_por_el_IdPublica()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "A"),
            Producto(2, 1002, "B"),
            Producto(3, 1003, "C")
        ]);

        var resultado = await servicio.GetAll("numero", "100");

        resultado.Select(p => p.IdPublica).Should().BeEquivalentTo([1001, 1002, 1003]);
    }

    [Fact]
    public async Task GetAll_por_numero_no_trae_productos_con_IdPublica_null()
    {
        var (servicio, _, _) = CrearCon([
            ProductoBuilder.Uno().ConId(1).ConIdPublica(null).ConNombre("Sin codigo").Build()
        ]);

        (await servicio.GetAll("numero", "10")).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_por_numero_es_el_unico_campo_sin_variante_desc()
    {
        var (servicio, _, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(() => servicio.GetAll("numerodesc", "100"));
    }

    // ------------------------------------------------------------------ campo "nombre" / "nombredesc"

    [Fact]
    public async Task GetAll_por_nombre_ordena_alfabeticamente_ascendente()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "Coca"),
            Producto(2, 1002, "Agua"),
            Producto(3, 1003, "Pepsi")
        ]);

        var resultado = await servicio.GetAll("nombre", null);

        resultado.Select(p => p.Nombre).Should().Equal("Agua", "Coca", "Pepsi");
    }

    [Fact]
    public async Task GetAll_por_nombredesc_ordena_alfabeticamente_descendente()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "Coca"),
            Producto(2, 1002, "Agua"),
            Producto(3, 1003, "Pepsi")
        ]);

        var resultado = await servicio.GetAll("nombredesc", null);

        resultado.Select(p => p.Nombre).Should().Equal("Pepsi", "Coca", "Agua");
    }

    [Fact]
    public async Task GetAll_por_nombre_hace_coincidencia_parcial()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "Gaseosa de Cola"),
            Producto(2, 1002, "Agua Mineral")
        ]);

        var resultado = await servicio.GetAll("nombre", "gaseosa");

        resultado.Should().ContainSingle();
    }

    // ------------------------------------------------------------------ campo "otro" = categoria

    [Fact]
    public async Task GetAll_por_otro_ordena_por_el_nombre_de_la_categoria()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "Gaseosa", Bebidas),
            Producto(2, 1002, "Empanada", Comidas),
            Producto(3, 1003, "Agua", Bebidas)
        ]);

        var resultado = await servicio.GetAll("otro", null);

        resultado.Select(p => p.Categoria).Should().Equal("Bebidas", "Bebidas", "Comidas");
    }

    [Fact]
    public async Task GetAll_por_otrodesc_ordena_por_el_nombre_de_la_categoria_descendente()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "Gaseosa", Bebidas),
            Producto(2, 1002, "Empanada", Comidas)
        ]);

        var resultado = await servicio.GetAll("otrodesc", null);

        resultado.Select(p => p.Categoria).Should().Equal("Comidas", "Bebidas");
    }

    [Fact]
    public async Task GetAll_por_otro_filtra_por_el_nombre_de_la_categoria()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "Gaseosa", Bebidas),
            Producto(2, 1002, "Empanada", Comidas)
        ]);

        var resultado = await servicio.GetAll("otro", "bebidas");

        resultado.Should().ContainSingle();
        resultado.Single().Nombre.Should().Be("Gaseosa");
    }

    [Fact]
    public async Task GetAll_por_otro_sin_coincidencia_devuelve_vacio()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "Gaseosa", Bebidas)]);

        (await servicio.GetAll("otro", "limpieza")).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ campo "proveedor"

    [Fact]
    public async Task GetAll_por_proveedor_ordena_alfabeticamente_ascendente()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "A", proveedor: "Zeta"),
            Producto(2, 1002, "B", proveedor: "Alfa")
        ]);

        var resultado = await servicio.GetAll("proveedor", null);

        resultado.Select(p => p.Proveedor).Should().Equal("Alfa", "Zeta");
    }

    [Fact]
    public async Task GetAll_por_proveedordesc_ordena_alfabeticamente_descendente()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "A", proveedor: "Zeta"),
            Producto(2, 1002, "B", proveedor: "Alfa")
        ]);

        var resultado = await servicio.GetAll("proveedordesc", null);

        resultado.Select(p => p.Proveedor).Should().Equal("Zeta", "Alfa");
    }

    [Fact]
    public async Task GetAll_por_proveedor_filtra_por_el_nombre_del_proveedor()
    {
        var (servicio, _, _) = CrearCon([
            Producto(1, 1001, "A", proveedor: "Distribuidora Norte"),
            Producto(2, 1002, "B", proveedor: "Distribuidora Sur")
        ]);

        var resultado = await servicio.GetAll("proveedor", "norte");

        resultado.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_por_proveedor_ignora_los_productos_sin_proveedor()
    {
        var (servicio, _, _) = CrearCon([
            ProductoBuilder.Uno().ConId(1).ConIdPublica(1001).ConNombre("A").ConProveedor(null).Build()
        ]);

        (await servicio.GetAll("proveedor", "norte")).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ resultado vacio

    [Fact]
    public async Task GetAll_sin_coincidencias_devuelve_vacio_y_no_un_null()
    {
        var (servicio, _, _) = CrearCon([Producto(1, 1001, "Gaseosa")]);

        var resultado = await servicio.GetAll("nombre", "inexistente");

        resultado.Should().NotBeNull();
        resultado.Should().BeEmpty();
    }
}