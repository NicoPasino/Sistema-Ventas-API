using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Infra.Data;
using NicoPasino.Infra.Repositorio;
using NicoPasino.Tests.Common;
using NicoPasino.Tests.Common.Builders;

namespace NicoPasino.Tests.Infraestructura;

/// <summary>
/// Tests de <see cref="RepositorioGenericoVentas{T}"/> contra un
/// <see cref="ventasdbContext"/> real con el proveedor <b>InMemory</b> (#30).
/// <para>
/// No se levanta MySQL: se agrega <c>Microsoft.EntityFrameworkCore.InMemory</c>
/// solo al proyecto de tests. Eso alcanza para ejercitar el LINQ real, el tracking
/// y el <c>Include</c>, que es lo que el fake no puede reproducir.
/// </para>
/// <para>
/// Se documenta ademas la <b>asimetria de tracking</b>
/// (<see cref="BugsConocidos.RepositorioAsNoTrackingAsimetrico"/>, issue #48): <c>GetAsync</c>
/// aplica <c>AsNoTracking()</c> solo si hay filtro, y <c>ListarAsync</c> nunca.
/// </para>
/// </summary>
public class RepositorioGenericoVentasTests
{
    private static ventasdbContext Contexto()
        => new(new DbContextOptionsBuilder<ventasdbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RepositorioGenericoVentas<T> RepoDe<T>(ventasdbContext ctx) where T : class
        => new(ctx);

    // ==================================================================== GetAll

    [Fact]
    public async Task GetAll_devuelve_todas_las_entidades()
    {
        using var ctx = Contexto();
        ctx.Categoria.AddRange(
            new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Comidas").Build());
        await ctx.SaveChangesAsync();

        var todas = await RepoDe<Categoria>(ctx).GetAll();

        todas.Should().HaveCount(2);
    }

    // ================================================================== GetById

    [Fact]
    public async Task GetById_devuelve_la_entidad_cuando_existe()
    {
        using var ctx = Contexto();
        ctx.Categoria.Add(new CategoriaBuilder().ConId(10).ConNombre("Bebidas").Build());
        await ctx.SaveChangesAsync();

        var encontrada = await RepoDe<Categoria>(ctx).GetById(10);

        encontrada.Should().NotBeNull();
        encontrada!.Nombre.Should().Be("Bebidas");
    }

    [Fact]
    public async Task GetById_devuelve_null_cuando_no_existe()
    {
        using var ctx = Contexto();

        var encontrada = await RepoDe<Categoria>(ctx).GetById(999);

        encontrada.Should().BeNull();
    }

    // ====================================================================== Add

    [Fact]
    public async Task Add_devuelve_la_entidad_y_la_persiste()
    {
        using var ctx = Contexto();
        var repo = RepoDe<Categoria>(ctx);

        var agregada = await repo.Add(new CategoriaBuilder().ConNombre("Bebidas").Build());

        agregada.Should().NotBeNull();
        (await ctx.Categoria.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddRange_persiste_todas_las_entidades()
    {
        using var ctx = Contexto();

        await RepoDe<Categoria>(ctx).AddRange([
            new CategoriaBuilder().ConNombre("Bebidas").Build(),
            new CategoriaBuilder().ConNombre("Comidas").Build()
        ]);

        (await ctx.Categoria.CountAsync()).Should().Be(2);
    }

    // =================================================================== Update

    [Fact]
    public async Task Update_devuelve_el_conteo_de_filas_de_SaveChanges()
    {
        using var ctx = Contexto();
        var categoria = new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build();
        ctx.Categoria.Add(categoria);
        await ctx.SaveChangesAsync();
        categoria.Nombre = "Gaseosas";

        var filas = await RepoDe<Categoria>(ctx).Update(categoria);

        filas.Should().Be(1);
    }

    [Fact]
    public async Task Update_persiste_los_cambios()
    {
        using var ctx = Contexto();
        var categoria = new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build();
        ctx.Categoria.Add(categoria);
        await ctx.SaveChangesAsync();
        categoria.Nombre = "Gaseosas";

        await RepoDe<Categoria>(ctx).Update(categoria);

        (await ctx.Categoria.AsNoTracking().SingleAsync()).Nombre.Should().Be("Gaseosas");
    }

    // =================================================================== Delete

    [Fact]
    public async Task Delete_elimina_la_entidad()
    {
        using var ctx = Contexto();
        var categoria = new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build();
        ctx.Categoria.Add(categoria);
        await ctx.SaveChangesAsync();

        await RepoDe<Categoria>(ctx).Delete(categoria);

        (await ctx.Categoria.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteRange_elimina_todas_las_entidades()
    {
        using var ctx = Contexto();
        var a = new CategoriaBuilder().ConId(1).ConNombre("A").Build();
        var b = new CategoriaBuilder().ConId(2).ConNombre("B").Build();
        ctx.Categoria.AddRange(a, b);
        await ctx.SaveChangesAsync();

        await RepoDe<Categoria>(ctx).DeleteRange([a, b]);

        (await ctx.Categoria.CountAsync()).Should().Be(0);
    }

    // ================================================================== GetAsync

    [Fact]
    public async Task GetAsync_sin_filtro_devuelve_el_primero()
    {
        using var ctx = Contexto();
        ctx.Categoria.AddRange(
            new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Comidas").Build());
        await ctx.SaveChangesAsync();

        var primera = await RepoDe<Categoria>(ctx).GetAsync();

        primera!.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_con_filtro_devuelve_la_que_matchea()
    {
        using var ctx = Contexto();
        ctx.Categoria.AddRange(
            new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Comidas").Build());
        await ctx.SaveChangesAsync();

        var encontrada = await RepoDe<Categoria>(ctx).GetAsync(c => c.Nombre == "Comidas");

        encontrada!.Id.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_sin_match_devuelve_null()
    {
        using var ctx = Contexto();
        ctx.Categoria.Add(new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build());
        await ctx.SaveChangesAsync();

        var encontrada = await RepoDe<Categoria>(ctx).GetAsync(c => c.Nombre == "NoExiste");

        encontrada.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_con_incluir_carga_la_navegacion()
    {
        using var ctx = Contexto();
        var cliente = new ClienteBuilder().ConId(1).ConNombre("Juan Perez").Build();
        ctx.Cliente.Add(cliente);
        ctx.Venta.Add(new VentaBuilder().ConId(1).ConCliente(cliente).Build());
        await ctx.SaveChangesAsync();

        var venta = await RepoDe<Venta>(ctx).GetAsync(filtro: v => v.Id == 1, incluir: "IdClienteNavigation");

        venta!.IdClienteNavigation.Should().NotBeNull();
        venta.IdClienteNavigation.Nombre.Should().Be("Juan Perez");
    }

    [Fact]
    public async Task GetAsync_con_varios_paths_separados_por_coma_carga_todos()
    {
        using var ctx = Contexto();
        var categoria = new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build();
        var producto = new ProductoBuilder().ConId(7).ConNombre("Gaseosa").ConCategoria(categoria).Build();
        var venta = new VentaBuilder().ConId(1).Build();
        ctx.Categoria.Add(categoria);
        ctx.Producto.Add(producto);
        ctx.Venta.Add(venta);
        var item = VentaporproductoBuilder.Uno().ConProducto(producto).Build();
        item.IdVenta = 1;
        ctx.Ventaporproducto.Add(item);
        await ctx.SaveChangesAsync();

        var fila = await RepoDe<Ventaporproducto>(ctx).GetAsync(
            f => f.IdVenta == 1, incluir: "IdProductoNavigation,IdVentaNavigation");

        fila!.IdProductoNavigation.Should().NotBeNull();
        fila.IdVentaNavigation.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_con_espacios_en_el_incluir_aplica_Trim()
    {
        using var ctx = Contexto();
        var categoria = new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build();
        ctx.Categoria.Add(categoria);
        ctx.Producto.Add(new ProductoBuilder().ConId(1).ConNombre("Gaseosa").ConCategoria(categoria).Build());
        await ctx.SaveChangesAsync();

        var producto = await RepoDe<Producto>(ctx).GetAsync(
            f => f.Id == 1, incluir: " IdCategoriaNavigation ");

        producto!.IdCategoriaNavigation.Should().NotBeNull();
    }

    /// <summary>
    /// Asimetria de tracking, lado <c>AsNoTracking</c>: <c>GetAsync</c> con filtro
    /// devuelve una instancia <b>desacoplada</b> del contexto. Mutarla y guardar no
    /// persiste nada.
    /// Referencia: <see cref="BugsConocidos.RepositorioAsNoTrackingAsimetrico"/> (// BUG: ver issue #48).
    /// </summary>
    [Fact]
    public async Task GetAsync_con_filtro_devuelve_una_instancia_desacoplada()
    {
        using var ctx = Contexto();
        ctx.Categoria.Add(new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build());
        await ctx.SaveChangesAsync();

        var leida = await RepoDe<Categoria>(ctx).GetAsync(c => c.Id == 1);
        leida!.Nombre = "Cambiado";
        await ctx.SaveChangesAsync();

        (await ctx.Categoria.AsNoTracking().SingleAsync()).Nombre.Should().Be("Bebidas");
    }

    /// <summary>
    /// Asimetria de tracking, lado trackeado: <c>GetAsync</c> <b>sin</b> filtro
    /// devuelve la instancia trackeada. Mutarla y guardar la persiste implicitamente,
    /// sin llamar a <c>Update</c>.
    /// Referencia: <see cref="BugsConocidos.RepositorioAsNoTrackingAsimetrico"/> (// BUG: ver issue #48).
    /// </summary>
    [Fact]
    public async Task GetAsync_sin_filtro_devuelve_la_instancia_trackeada()
    {
        using var ctx = Contexto();
        ctx.Categoria.Add(new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build());
        await ctx.SaveChangesAsync();

        var leida = await RepoDe<Categoria>(ctx).GetAsync();
        leida!.Nombre = "Cambiado";
        await ctx.SaveChangesAsync();

        (await ctx.Categoria.AsNoTracking().SingleAsync()).Nombre.Should().Be("Cambiado");
    }

    // =============================================================== ListarAsync

    [Fact]
    public async Task ListarAsync_sin_filtro_ni_orden_devuelve_todas()
    {
        using var ctx = Contexto();
        ctx.Categoria.AddRange(
            new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Comidas").Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Categoria>(ctx).ListarAsync();

        lista.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListarAsync_con_filtro_solo_devuelve_las_que_matchean()
    {
        using var ctx = Contexto();
        ctx.Categoria.AddRange(
            new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Comidas").Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Categoria>(ctx).ListarAsync(filtro: c => c.Nombre == "Bebidas");

        lista.Should().ContainSingle();
        lista.Single().Nombre.Should().Be("Bebidas");
    }

    [Fact]
    public async Task ListarAsync_ordena_ascendente()
    {
        using var ctx = Contexto();
        ctx.Categoria.AddRange(
            new CategoriaBuilder().ConId(1).ConNombre("Zeta").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Alfa").Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Categoria>(ctx).ListarAsync(orden: q => q.OrderBy(c => c.Nombre));

        lista.Select(c => c.Nombre).Should().Equal("Alfa", "Zeta");
    }

    [Fact]
    public async Task ListarAsync_ordena_descendente()
    {
        using var ctx = Contexto();
        ctx.Categoria.AddRange(
            new CategoriaBuilder().ConId(1).ConNombre("Alfa").Build(),
            new CategoriaBuilder().ConId(2).ConNombre("Zeta").Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Categoria>(ctx).ListarAsync(orden: q => q.OrderByDescending(c => c.Nombre));

        lista.Select(c => c.Nombre).Should().Equal("Zeta", "Alfa");
    }

    [Fact]
    public async Task ListarAsync_ordena_por_un_campo_de_la_navegacion()
    {
        using var ctx = Contexto();
        var bebidas = new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build();
        var comidas = new CategoriaBuilder().ConId(2).ConNombre("Comidas").Build();
        ctx.Categoria.AddRange(bebidas, comidas);
        ctx.Producto.AddRange(
            new ProductoBuilder().ConId(1).ConNombre("Zeta").ConCategoria(bebidas).Build(),
            new ProductoBuilder().ConId(2).ConNombre("Alfa").ConCategoria(comidas).Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Producto>(ctx).ListarAsync(
            incluir: "IdCategoriaNavigation",
            orden: q => q.OrderBy(p => p.IdCategoriaNavigation.Nombre));

        lista.Select(p => p.IdCategoriaNavigation.Nombre).Should().Equal("Bebidas", "Comidas");
    }

    [Fact]
    public async Task ListarAsync_con_orden_null_no_rompe()
    {
        using var ctx = Contexto();
        ctx.Categoria.Add(new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Categoria>(ctx).ListarAsync(orden: null);

        lista.Should().ContainSingle();
    }

    [Fact]
    public async Task ListarAsync_con_incluir_carga_la_navegacion()
    {
        using var ctx = Contexto();
        var categoria = new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build();
        ctx.Categoria.Add(categoria);
        ctx.Producto.Add(new ProductoBuilder().ConId(1).ConNombre("Gaseosa").ConCategoria(categoria).Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Producto>(ctx).ListarAsync(incluir: "IdCategoriaNavigation");

        lista.Single().IdCategoriaNavigation.Should().NotBeNull();
    }

    /// <summary>
    /// Asimetria de tracking, lado <c>ListarAsync</c>: <b>nunca</b> aplica
    /// <c>AsNoTracking()</c>, asi que la entidad queda trackeada y una mutacion se
    /// persiste sola.
    /// Referencia: <see cref="BugsConocidos.RepositorioAsNoTrackingAsimetrico"/> (// BUG: ver issue #48).
    /// </summary>
    [Fact]
    public async Task ListarAsync_nunca_aplica_AsNoTracking()
    {
        using var ctx = Contexto();
        ctx.Categoria.Add(new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build());
        await ctx.SaveChangesAsync();

        var lista = await RepoDe<Categoria>(ctx).ListarAsync();
        lista.Single().Nombre = "Cambiado";
        await ctx.SaveChangesAsync();

        (await ctx.Categoria.AsNoTracking().SingleAsync()).Nombre.Should().Be("Cambiado");
    }
}
