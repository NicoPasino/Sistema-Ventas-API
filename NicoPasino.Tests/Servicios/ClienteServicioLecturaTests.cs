using FluentAssertions;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de <c>ClienteServicio.GetAll(bool)</c> y <c>GetAll(campo, valor)</c> (#19).
/// Mismo bug que en Producto: el filtro por <c>Activo</c> esta comentado, asi que el
/// parametro se ignora y siempre vuelven los inactivos.
/// Referencia: bug #39.
/// </summary>
public class ClienteServicioLecturaTests
{
    private static Cliente Cliente(int id, int documento, string nombre, string correo = "cliente@correo.com",
        string? telefono = "1122334455", bool activo = true, DateTime? fechaCreacion = null, Venta[] ventas = null!)
        => ClienteBuilder.Uno()
            .ConId(id)
            .ConDocumento(documento)
            .ConNombre(nombre)
            .ConCorreo(correo)
            .ConTelefono(telefono)
            .ConActivo(activo)
            .ConFechaCreacion(fechaCreacion ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .ConCompras(ventas ?? [])
            .Build();

    private static (ClienteServicio Servicio, RepositorioGenericoVentasFake<Cliente> Repo)
        CrearCon(params Cliente[] clientes)
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>(clientes.Length > 0 ? clientes : [Cliente(1, 30111222, "Ana Gomez")]);
        return (new ClienteServicio(repo, new ClienteValidador(repo)), repo);
    }

    /// <summary>Repositorio sin ningun cliente, para el caso "no hay nada que devolver".</summary>
    private static (ClienteServicio Servicio, RepositorioGenericoVentasFake<Cliente> Repo) CrearVacio()
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>();
        return (new ClienteServicio(repo, new ClienteValidador(repo)), repo);
    }

    // ------------------------------------------------------------------ GetAll(bool)

    [Fact]
    public async Task GetAll_devuelve_todos_los_clientes()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana Gomez"),
            Cliente(2, 33111222, "Beto Diaz"));

        var resultado = await servicio.GetAll(true);

        resultado.Should().HaveCount(2);
    }

    /// <summary>
    /// Caracteriza el bug: la linea 24 tiene el filtro comentado
    /// (<c>//filtro: m =&gt; m.Activo == activo</c>), asi que pedir solo activos
    /// tambien trae los inactivos. Contraste con el mensaje de error del
    /// repositorio, que tampoco se dispara.
    /// Referencia: bug #39.
    /// </summary>
    [Fact]
    public async Task GetAll_ignora_el_parametro_activo_y_devuelve_los_inactivos()
    {
        var (servicio, repo) = CrearCon(
            Cliente(1, 30111222, "Ana Gomez", activo: true),
            Cliente(2, 33111222, "Beto Diaz", activo: false));

        var resultado = await servicio.GetAll(true);

        resultado.Should().HaveCount(2);
        resultado.Select(c => c.Activo).Should().BeEquivalentTo([true, false]);
        repo.UltimoFiltro.Should().BeNull("no se pasa ningun filtro");
    }

    [Fact]
    public async Task GetAll_pedir_solo_inactivos_tambien_devuelve_los_activos()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana Gomez", activo: true),
            Cliente(2, 33111222, "Beto Diaz", activo: false));

        (await servicio.GetAll(false)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_ordena_por_fecha_de_creacion_descendente()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Antiguo", fechaCreacion: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Cliente(2, 33111222, "Reciente", fechaCreacion: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Cliente(3, 34111222, "Medio", fechaCreacion: new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var resultado = await servicio.GetAll(true);

        resultado.Select(c => c.Nombre).Should().Equal("Reciente", "Medio", "Antiguo");
    }

    [Fact]
    public async Task GetAll_carga_la_navegacion_Venta()
    {
        var (servicio, repo) = CrearCon();

        await servicio.GetAll(true);

        repo.UltimoInclude.Should().Be("Venta");
    }

    [Fact]
    public async Task GetAll_calcula_el_numero_de_compras_desde_la_navegacion()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez", ventas: [
            VentaBuilder.Uno().ConId(1).Build(),
            VentaBuilder.Uno().ConId(2).Build(),
            VentaBuilder.Uno().ConId(3).Build()
        ]));

        var resultado = await servicio.GetAll(true);

        resultado.Single().NroCompras.Should().Be(3, "Mapster: src.Venta.Count()");
    }

    [Fact]
    public async Task GetAll_devuelve_cero_compras_si_no_tiene_ventas()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez"));

        (await servicio.GetAll(true)).Single().NroCompras.Should().Be(0);
    }

    [Fact]
    public async Task GetAll_sin_clientes_devuelve_una_coleccion_vacia_y_no_un_null()
    {
        var (servicio, _) = CrearVacio();

        var resultado = await servicio.GetAll(true);

        resultado.Should().NotBeNull();
        resultado.Should().BeEmpty();
    }

    /// <summary>
    /// Contraste con <c>ProductoServicio.GetAll</c>, que envuelve toda excepcion en
    /// <c>new Exception(ex.Message)</c> y pierde el tipo. Aca la excepcion del
    /// repositorio sube intacta. Mismo comportamiento en <c>ProductoServicio.Update</c>.
    /// Referencia: bug #47.
    /// </summary>
    [Fact]
    public async Task GetAll_propaga_la_excepcion_del_repositorio_sin_envolverla()
    {
        var (servicio, repo) = CrearCon();
        repo.AlListarAsync = () => throw new InvalidOperationException("se cayo la base");

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.GetAll(true));

        excepcion.Message.Should().Be("se cayo la base");
    }

    // ------------------------------------------------------------------ GetAll(campo, valor)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_rechaza_un_campo_de_busqueda_vacio(string? campo)
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetAll(campo!, "valor"));

        excepcion.Message.Should().Contain("Campo de búsqueda no válido.");
        repo.VecesListar.Should().Be(0);
    }

    [Fact]
    public async Task GetAll_rechaza_un_campo_no_soportado()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetAll("apellido", "x"));

        excepcion.Message.Should().Be(
            "Campo de búsqueda 'apellido' no soportado. Campos soportados: numero(Documento), nombre, otro(Correo).");
    }

    [Fact]
    public async Task GetAll_normaliza_el_campo_y_el_valor()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez"));

        (await servicio.GetAll("  NOMBRE  ", "  ANA  ")).Should().ContainSingle();
    }

    /// <summary>
    /// A diferencia de Producto, los filtros de Cliente <b>no</b> arrancan con
    /// <c>m.Activo</c>, asi que con valor tambien entran los inactivos. Caracteriza
    /// que el unico filtro por <c>Activo</c> en toda la clase es el del
    /// <c>GetAll(bool)</c> comentado.
    /// </summary>
    [Fact]
    public async Task GetAll_con_valor_tambien_devuelve_los_inactivos()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana Gomez", activo: true),
            Cliente(2, 33111222, "Ana Retirada", activo: false));

        (await servicio.GetAll("nombre", "ana")).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_por_numero_ordena_por_Documento_ascendente()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 33111222, "Beto"),
            Cliente(2, 30111222, "Ana"),
            Cliente(3, 31111222, "Caro"));

        var resultado = await servicio.GetAll("numero", null);

        resultado.Select(c => c.Documento).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetAll_por_numerodesc_ordena_por_Documento_descendente()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana"),
            Cliente(2, 33111222, "Beto"));

        var resultado = await servicio.GetAll("numerodesc", null);

        resultado.Select(c => c.Documento).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetAll_por_numero_hace_coincidencia_parcial_del_Documento()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana"),
            Cliente(2, 30119999, "Beto"),
            Cliente(3, 33111222, "Caro"));

        var resultado = await servicio.GetAll("numero", "3011");

        resultado.Should().HaveCount(2);
        resultado.Select(c => c.Documento).Should().BeEquivalentTo([30111222, 30119999]);
    }

    [Fact]
    public async Task GetAll_por_nombre_ordena_alfabeticamente_ascendente()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Caro Diaz"),
            Cliente(2, 33111222, "Ana Gomez"),
            Cliente(3, 31111222, "Beto Ruiz"));

        var resultado = await servicio.GetAll("nombre", null);

        resultado.Select(c => c.Nombre).Should().Equal("Ana Gomez", "Beto Ruiz", "Caro Diaz");
    }

    [Fact]
    public async Task GetAll_por_nombredesc_ordena_alfabeticamente_descendente()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Caro Diaz"),
            Cliente(2, 33111222, "Ana Gomez"));

        var resultado = await servicio.GetAll("nombredesc", null);

        resultado.Select(c => c.Nombre).Should().Equal("Caro Diaz", "Ana Gomez");
    }

    [Fact]
    public async Task GetAll_por_nombre_compara_en_minusculas()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez"));

        // El servicio pasa el valor a minusculas y compara con ToLower(): "GOMEZ"
        // encuentra "Gomez". Ojo que no es sensible a acentos, porque ToLower no
        // quita diacriticos: "GÓMEZ" no encuentra "Gomez".
        (await servicio.GetAll("nombre", "GOMEZ")).Should().ContainSingle();
        (await servicio.GetAll("nombre", "GÓMEZ")).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_por_otro_ordena_por_el_Correo()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana", correo: "zoe@correo.com"),
            Cliente(2, 33111222, "Beto", correo: "ana@correo.com"));

        var resultado = await servicio.GetAll("otro", null);

        resultado.Select(c => c.Correo).Should().Equal("ana@correo.com", "zoe@correo.com");
    }

    [Fact]
    public async Task GetAll_por_otrodesc_ordena_por_el_Correo_descendente()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana", correo: "ana@correo.com"),
            Cliente(2, 33111222, "Beto", correo: "zoe@correo.com"));

        var resultado = await servicio.GetAll("otrodesc", null);

        resultado.Select(c => c.Correo).Should().Equal("zoe@correo.com", "ana@correo.com");
    }

    [Fact]
    public async Task GetAll_por_otro_filtra_por_el_Correo()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana", correo: "ana@correo.com"),
            Cliente(2, 33111222, "Beto", correo: "beto@correo.com"));

        (await servicio.GetAll("otro", "beto@")).Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_sin_valor_no_arma_filtro_pero_si_ordena(string? valor)
    {
        var (servicio, repo) = CrearCon(Cliente(1, 30111222, "Ana"), Cliente(2, 33111222, "Beto"));

        var resultado = await servicio.GetAll("nombre", valor);

        resultado.Should().HaveCount(2);
        repo.UltimoFiltro.Should().BeNull();
    }

    [Fact]
    public async Task GetAll_sin_coincidencias_devuelve_vacio()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez"));

        var resultado = await servicio.GetAll("nombre", "inexistente");

        resultado.Should().NotBeNull();
        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_calcula_NroCompras_tambien_en_la_busqueda_por_campo()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez", ventas: [
            VentaBuilder.Uno().ConId(1).Build()
        ]));

        (await servicio.GetAll("nombre", "ana")).Single().NroCompras.Should().Be(1);
    }

    // ------------------------------------------------------------------ GetById

    [Fact]
    public async Task GetById_devuelve_el_cliente_por_Documento()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana Gomez"),
            Cliente(2, 33111222, "Beto Diaz"));

        var resultado = await servicio.GetById(33111222);

        resultado.Documento.Should().Be(33111222);
        resultado.Nombre.Should().Be("Beto Diaz");
    }

    [Fact]
    public async Task GetById_carga_la_navegacion_Venta()
    {
        var (servicio, repo) = CrearCon();

        await servicio.GetById(30111222);

        repo.UltimoInclude.Should().Be("Venta");
        repo.VecesGetAsync.Should().Be(1);
    }

    [Fact]
    public async Task GetById_calcula_el_numero_de_compras()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez", ventas: [
            VentaBuilder.Uno().ConId(1).Build(),
            VentaBuilder.Uno().ConId(2).Build()
        ]));

        (await servicio.GetById(30111222)).NroCompras.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9999999)]
    [InlineData(100000000)]
    public async Task GetById_rechaza_un_documento_que_no_tiene_ocho_digitos(int documento)
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetById(documento));

        excepcion.Message.Should().Be("El documento debe tener 8 dígitos.");
        repo.VecesGetAsync.Should().Be(0, "valida antes de consultar");
    }

    /// <summary>
    /// Un documento inexistente devuelve <c>null</c>, para que el controller pueda
    /// distinguir "no existe" y responder 404 (#35).
    /// </summary>
    [Fact]
    public async Task GetById_inexistente_devuelve_null()
    {
        var (servicio, _) = CrearCon(Cliente(1, 30111222, "Ana Gomez"));

        var resultado = await servicio.GetById(33111222);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task GetById_no_devuelve_el_cliente_de_otro_documento()
    {
        var (servicio, _) = CrearCon(
            Cliente(1, 30111222, "Ana Gomez", correo: "ana@correo.com"),
            Cliente(2, 33111222, "Beto Diaz", correo: "beto@correo.com"));

        var resultado = await servicio.GetById(33111222);

        resultado.Correo.Should().Be("beto@correo.com");
    }
}