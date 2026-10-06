using FluentAssertions;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Validadores;

/// <summary>
/// Tests de <c>ProductoValidador.ValidarCategoriaAsync</c> (#9), el unico metodo
/// del validador que consulta el repositorio. El fake permite ademas verificar
/// <b>que filtro le paso al repositorio</b>, que es lo que evita el clasico
/// "trae todas y filtra en memoria".
/// </summary>
public class ProductoValidadorValidarCategoriaTests
{
    private static (ProductoValidador Validador, RepositorioGenericoVentasFake<Categoria> Repo) CrearCon(params Categoria[] categorias)
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>(categorias);
        return (new ProductoValidador(repo), repo);
    }

    [Fact]
    public async Task Acepta_una_categoria_que_existe()
    {
        var (validador, _) = CrearCon(new CategoriaBuilder().ConId(7).ConNombre("Bebidas").Build());

        await validador.ValidarCategoriaAsync(7);
    }

    [Fact]
    public async Task Rechaza_una_categoria_que_no_existe()
    {
        var (validador, _) = CrearCon(new CategoriaBuilder().ConId(1).Build());

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarCategoriaAsync(999));

        excepcion.Message.Should().Be("La categoría indicada no existe.");
    }

    [Fact]
    public async Task Rechaza_una_categoria_inexistente_si_el_repositorio_esta_vacio()
    {
        var (validador, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarCategoriaAsync(1));

        excepcion.Message.Should().Be("La categoría indicada no existe.");
    }

    [Fact]
    public async Task Consulta_el_repositorio_con_el_filtro_por_Id()
    {
        var (validador, repo) = CrearCon(new CategoriaBuilder().ConId(7).Build());

        await validador.ValidarCategoriaAsync(7);

        repo.VecesGetAsync.Should().Be(1);
        repo.UltimoFiltro.Should().NotBeNull();
    }

    [Fact]
    public async Task No_pasa_la_ruta_de_Include_porque_solo_necesita_el_Id()
    {
        var (validador, repo) = CrearCon(new CategoriaBuilder().ConId(7).Build());

        await validador.ValidarCategoriaAsync(7);

        repo.UltimoInclude.Should().BeEmpty();
    }

    [Fact]
    public async Task Acepta_la_primera_de_varias_categorias_con_el_mismo_nombre()
    {
        var (validador, _) = CrearCon(
            new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Bebidas").Build()
        );

        await validador.ValidarCategoriaAsync(2);
    }

    [Fact]
    public async Task Distingue_entre_categorias_con_el_mismo_Id_pero_distinto_nombre()
    {
        var (validador, _) = CrearCon(
            new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build()
        );

        await validador.ValidarCategoriaAsync(1);
        await Assert.ThrowsAsync<DataException>(() => validador.ValidarCategoriaAsync(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rechaza_una_categoria_con_una_Clave_invalida(int id)
    {
        var (validador, repo) = CrearCon(new CategoriaBuilder().ConId(1).Build());

        await Assert.ThrowsAsync<DataException>(() => validador.ValidarCategoriaAsync(id));

        repo.VecesGetAsync.Should().Be(1, "la consulta se hace igual y falla el filtro en memoria");
    }
}