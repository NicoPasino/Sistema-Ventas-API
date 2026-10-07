using FluentAssertions;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de <c>CategoriaServicio</c> (#24). Es el servicio mas simple de los cuatro:
/// solo 2 de los 6 metodos de la interfaz estan implementados (<c>GetAll(bool)</c> y
/// <c>GetById</c>). El resto tira <c>NotImplementedException</c>, que es como decir
/// que la API de categorias es de <b>solo lectura</b> (en el controller solo hay
/// endpoints GET).
/// </summary>
public class CategoriaServicioTests
{
    private static (CategoriaServicio Servicio, RepositorioGenericoVentasFake<Categoria> Repo)
        CrearCon(params Categoria[] categorias)
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>(
            categorias.Length > 0 ? categorias : [CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build()]);
        return (new CategoriaServicio(repo), repo);
    }

    private static (CategoriaServicio Servicio, RepositorioGenericoVentasFake<Categoria> Repo) CrearVacio()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();
        return (new CategoriaServicio(repo), repo);
    }

    [Fact]
    public void Constructor_con_repo_null_lanza_ArgumentNullException()
    {
        var excepcion = Assert.Throws<ArgumentNullException>(() => new CategoriaServicio(null!));

        excepcion.ParamName.Should().Be("repoG");
    }

    // ------------------------------------------------------------------ GetAll(bool)

    [Fact]
    public async Task GetAll_devuelve_las_categorias_mapeadas()
    {
        var (servicio, _) = CrearCon(
            CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build(),
            CategoriaBuilder.Uno().ConId(2).ConNombre("Lacteos").Build());

        var resultado = await servicio.GetAll(true);

        resultado.Select(c => (c.Id, c.Nombre))
            .Should().Equal((1, "Bebidas"), (2, "Lacteos"));
    }

    [Fact]
    public async Task GetAll_ordena_por_nombre_ascendente()
    {
        var (servicio, repo) = CrearCon(
            CategoriaBuilder.Uno().ConId(1).ConNombre("Lacteos").Build(),
            CategoriaBuilder.Uno().ConId(2).ConNombre("Alimentos").Build(),
            CategoriaBuilder.Uno().ConId(3).ConNombre("Bebidas").Build());

        var resultado = await servicio.GetAll(true);

        repo.OrdenEsperado().Select(c => c.Nombre).Should().Equal("Alimentos", "Bebidas", "Lacteos");
        resultado.Select(c => c.Nombre).Should().Equal("Alimentos", "Bebidas", "Lacteos");
    }

    [Fact]
    public async Task GetAll_sin_datos_devuelve_una_coleccion_vacia_y_no_un_null()
    {
        var (servicio, _) = CrearVacio();

        var resultado = await servicio.GetAll(true);

        resultado.Should().NotBeNull();
        resultado.Should().BeEmpty();
    }

    /// <summary>
    /// Caracteriza que <c>activo</c> se ignora: el servicio nunca arma un filtro por
    /// <c>Activo</c> (ni siquiera comentado como en Producto/Cliente, directamente no
    /// existe), asi que pedir activos o inactivos devuelve exactamente lo mismo.
    /// </summary>
    [Fact]
    public async Task GetAll_ignora_el_parametro_activo()
    {
        var (servicio, repo) = CrearCon(CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build());

        var activos = await servicio.GetAll(true);
        var inactivos = await servicio.GetAll(false);

        activos.Should().BeEquivalentTo(inactivos);
        repo.UltimoFiltro.Should().BeNull("nunca se pasa filtro");
        repo.UltimoInclude.Should().Be("");
    }

    // ------------------------------------------------------------------ GetById

    [Fact]
    public async Task GetById_devuelve_la_categoria_encontrada()
    {
        var (servicio, _) = CrearCon(
            CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build(),
            CategoriaBuilder.Uno().ConId(2).ConNombre("Lacteos").Build());

        var resultado = await servicio.GetById(2);

        resultado.Id.Should().Be(2);
        resultado.Nombre.Should().Be("Lacteos");
    }

    [Fact]
    public async Task GetById_no_devuelve_la_categoria_de_otra_id()
    {
        var (servicio, _) = CrearCon(
            CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build(),
            CategoriaBuilder.Uno().ConId(2).ConNombre("Lacteos").Build());

        (await servicio.GetById(1)).Nombre.Should().Be("Bebidas");
    }

    [Fact]
    public async Task GetById_consulta_por_la_PK_Id_sin_include()
    {
        var (servicio, repo) = CrearCon();

        await servicio.GetById(1);

        repo.VecesGetAsync.Should().Be(1);
        repo.UltimoInclude.Should().Be("");
    }

    /// <summary>
    /// Caracteriza que una categoria inexistente devuelve un <c>CategoriaDto</c>
    /// vacio con <c>Id == 0</c>, no <c>null</c> ni excepcion. Es el caso que el
    /// controller distingue como 404 (<c>Ventas.Productos.cs:134</c> usa
    /// <c>obj?.Id &gt; 0</c>), distinto del patron roto de Cliente (#35) donde esa
    /// rama queda muerta.
    /// </summary>
    [Fact]
    public async Task GetById_inexistente_devuelve_un_DTO_vacio_con_Id_cero()
    {
        var (servicio, _) = CrearCon(CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build());

        var resultado = await servicio.GetById(999);

        resultado.Should().NotBeNull();
        resultado.Id.Should().Be(0);
        resultado.Nombre.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetById_rechaza_un_id_menor_a_uno(int id)
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.GetById(id));

        excepcion.Message.Should().Be("Id no válido.");
        repo.VecesGetAsync.Should().Be(0, "valida antes de consultar");
    }

    // ------------------------------------------------- metodos no implementados

    /// <summary>
    /// Caracteriza que la API de categorias es solo lectura: <c>GetAll(campo, valor)</c>
    /// no esta implementado.
    /// </summary>
    [Fact]
    public async Task GetAll_por_campo_lanza_NotImplementedException()
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<NotImplementedException>(
            () => servicio.GetAll("nombre", "valor"));

        excepcion.Message.Should().NotBeNullOrWhiteSpace("es un NotImplementedException sin mensaje propio: .NET pone el texto por defecto");
        repo.VecesListar.Should().Be(0);
    }

    [Fact]
    public async Task Create_lanza_NotImplementedException()
    {
        var (servicio, _) = CrearCon();
        var dto = new CategoriaDto { Id = 0, Nombre = "Bebidas" };

        var excepcion = await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Create(dto));

        excepcion.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Update_lanza_NotImplementedException()
    {
        var (servicio, _) = CrearCon();
        var dto = new CategoriaDto { Id = 1, Nombre = "Bebidas" };

        var excepcion = await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Update(dto));

        excepcion.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Enable_lanza_NotImplementedException()
    {
        var (servicio, _) = CrearCon();
        Action actuacion = () => servicio.Enable(1, false);

        var excepcion = Record.Exception(actuacion);

        excepcion.Should().BeOfType<NotImplementedException>();
        excepcion!.Message.Should().NotBeNullOrWhiteSpace();
    }
}