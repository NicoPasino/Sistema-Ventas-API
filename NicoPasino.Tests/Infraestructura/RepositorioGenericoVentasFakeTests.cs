using FluentAssertions;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Infraestructura;

/// <summary>
/// Tests del fake (#5). Ademas de verificar cada metodo, cubren las dos decisiones
/// de diseno que hacen util al fake:
/// <list type="number">
///   <item><description>los filtros y el orden <b>se evaluan de verdad</b> (no se comparan por referencia);</description></item>
///   <item><description><c>GetAsync</c> devuelve copia y <c>ListarAsync</c> devuelve la instancia real,
///   igual que el <c>AsNoTracking()</c> condicional de <c>RepositorioGenericoVentas.cs:59</c>.</description></item>
/// </list>
/// </summary>
public class RepositorioGenericoVentasFakeTests
{
    private static RepositorioGenericoVentasFake<Categoria> ConTres() => new(
        new CategoriaBuilder().ConId(1).ConNombre("Bebidas").Build(),
        new CategoriaBuilder().ConId(2).ConNombre("Comidas").Build(),
        new CategoriaBuilder().ConId(3).ConNombre("Limpieza").Build()
    );

    // ------------------------------------------------------------------ Add

    [Fact]
    public async Task Add_guarda_la_entidad_y_la_devuelve()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();
        var nueva = new CategoriaBuilder().ConNombre("Bebidas").Build();

        var resultado = await repo.Add(nueva);

        resultado.Should().BeSameAs(nueva);
        repo.Entidades.Should().ContainSingle();
        repo.VecesAdd.Should().Be(1);
        repo.UltimaEntidadAgregada.Should().BeSameAs(nueva);
    }

    [Fact]
    public async Task Add_le_asigna_una_pk_autoincremental_si_esta_en_cero()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();
        var a = new CategoriaBuilder().ConNombre("A").Build();
        var b = new CategoriaBuilder().ConNombre("B").Build();

        await repo.Add(a);
        await repo.Add(b);

        a.Id.Should().NotBe(0);
        b.Id.Should().NotBe(0);
        b.Id.Should().NotBe(a.Id);
    }

    [Fact]
    public async Task Add_no_pisa_una_pk_que_ya_venia_asignada()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();
        var conId = new CategoriaBuilder().ConId(77).ConNombre("A").Build();

        await repo.Add(conId);

        conId.Id.Should().Be(77);
    }

    [Fact]
    public async Task Add_con_el_gancho_AlAdd_no_toca_el_almacen()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();
        var sustituida = new CategoriaBuilder().ConId(99).ConNombre("Sustituida").Build();
        repo.AlAdd = _ => sustituida;

        var resultado = await repo.Add(new CategoriaBuilder().ConNombre("Original").Build());

        resultado.Should().BeSameAs(sustituida);
        repo.Entidades.Should().BeEmpty();
        repo.VecesAdd.Should().Be(1);
    }

    // ------------------------------------------------------------------ AddRange

    [Fact]
    public async Task AddRange_guarda_todas_las_entidades()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();

        await repo.AddRange([
            new CategoriaBuilder().ConNombre("A").Build(),
            new CategoriaBuilder().ConNombre("B").Build(),
            new CategoriaBuilder().ConNombre("C").Build()
        ]);

        repo.Entidades.Select(e => e.Nombre).Should().BeEquivalentTo(["A", "B", "C"]);
        repo.VecesAdd.Should().Be(3);
        repo.UltimaEntidadAgregada!.Nombre.Should().Be("C");
    }

    [Fact]
    public async Task AddRange_con_una_lista_vacia_no_hace_nada()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();

        await repo.AddRange([]);

        repo.Entidades.Should().BeEmpty();
        repo.VecesAdd.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task AddRange_asigna_pks_correlativas(int cantidad)
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();
        var entidades = Enumerable.Range(1, cantidad)
            .Select(i => new CategoriaBuilder().ConNombre($"C{i}").Build())
            .ToArray();

        await repo.AddRange(entidades);

        repo.Entidades.Select(e => e.Id).Should().BeEquivalentTo(Enumerable.Range(1, cantidad));
    }

    // ------------------------------------------------------------------ Update

    [Fact]
    public async Task Update_devuelve_1_filas_afectadas_por_defecto()
    {
        var repo = ConTres();

        var resultado = await repo.Update(repo.Entidades[0]);

        resultado.Should().Be(1);
        repo.VecesUpdate.Should().Be(1);
        repo.UltimaEntidadActualizada.Should().BeSameAs(repo.Entidades[0]);
    }

    [Fact]
    public async Task Update_respeta_el_valor_de_ResultadoUpdate()
    {
        var repo = ConTres();
        repo.ResultadoUpdate = 0;

        (await repo.Update(repo.Entidades[0])).Should().Be(0);
    }

    [Fact]
    public async Task Update_consume_la_cola_ResultadosUpdate_en_orden()
    {
        var repo = ConTres();
        repo.ResultadosUpdate.Enqueue(0);
        repo.ResultadosUpdate.Enqueue(1);

        (await repo.Update(repo.Entidades[0])).Should().Be(0);
        (await repo.Update(repo.Entidades[1])).Should().Be(1);
        (await repo.Update(repo.Entidades[2])).Should().Be(1, "la cola se agota y vuelve el valor por defecto");
    }

    [Fact]
    public async Task Update_propaga_los_valores_a_la_entidad_almacenada()
    {
        var repo = ConTres();
        var almacenada = repo.Entidades[1];

        // GetAsync devuelve una copia: se muta la copia y se manda a Update.
        var copia = await repo.GetAsync(filtro: c => c.Id == 2);
        copia!.Nombre = "Comidas Rapidas";
        await repo.Update(copia);

        almacenada.Nombre.Should().Be("Comidas Rapidas");
    }

    [Fact]
    public async Task Update_de_una_entidad_inexistente_no_la_agrega_al_almacen()
    {
        var repo = ConTres();

        await repo.Update(new CategoriaBuilder().ConId(999).ConNombre("Fantasma").Build());

        repo.Entidades.Should().HaveCount(3);
        repo.Entidades.Should().NotContain(e => e.Nombre == "Fantasma");
    }

    // ------------------------------------------------------------------ Delete

    [Fact]
    public async Task Delete_quita_la_entidad_del_almacen()
    {
        var repo = ConTres();
        var objetivo = repo.Entidades[1];

        await repo.Delete(objetivo);

        repo.Entidades.Select(e => e.Nombre).Should().BeEquivalentTo(["Bebidas", "Limpieza"]);
        repo.VecesDelete.Should().Be(1);
        repo.UltimaEntidadEliminada.Should().BeSameAs(objetivo);
    }

    [Fact]
    public async Task Delete_encuentra_la_entidad_por_Id_aunque_le_pasen_una_copia()
    {
        var repo = ConTres();

        var copia = await repo.GetAsync(filtro: c => c.Id == 2);
        await repo.Delete(copia!);

        repo.Entidades.Select(e => e.Id).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public async Task Delete_de_una_entidad_inexistente_no_falla()
    {
        var repo = ConTres();

        await repo.Delete(new CategoriaBuilder().ConId(999).Build());

        repo.Entidades.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeleteRange_quita_todas_las_entidades_de_la_lista()
    {
        var repo = ConTres();

        await repo.DeleteRange([repo.Entidades[0], repo.Entidades[2]]);

        repo.Entidades.Select(e => e.Nombre).Should().BeEquivalentTo(["Comidas"]);
        repo.VecesDelete.Should().Be(2);
    }

    // ------------------------------------------------------------------ GetById

    [Fact]
    public async Task GetById_encuentra_la_entidad_por_su_pk()
    {
        var repo = ConTres();

        var encontrada = await repo.GetById(2);

        encontrada.Should().NotBeNull();
        encontrada!.Nombre.Should().Be("Comidas");
        repo.VecesGetById.Should().Be(1);
    }

    [Fact]
    public async Task GetById_devuelve_null_si_no_existe()
    {
        var repo = ConTres();

        (await repo.GetById(999)).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetById_con_una_pk_invalida_devuelve_null(int id)
    {
        var repo = ConTres();

        (await repo.GetById(id)).Should().BeNull();
    }

    // ------------------------------------------------------------------ GetAll

    [Fact]
    public async Task GetAll_devuelve_todas_las_entidades()
    {
        var repo = ConTres();

        var todas = await repo.GetAll();

        todas.Select(e => e!.Nombre).Should().BeEquivalentTo(["Bebidas", "Comidas", "Limpieza"]);
        repo.VecesGetAll.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_sobre_un_almacen_vacio_devuelve_una_coleccion_vacia()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();

        (await repo.GetAll()).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ GetAsync

    [Fact]
    public async Task GetAsync_sin_filtro_devuelve_la_primera_entidad()
    {
        var repo = ConTres();

        var resultado = await repo.GetAsync();

        resultado.Nombre.Should().Be("Bebidas");
        repo.VecesGetAsync.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_evalua_de_verdad_el_filtro()
    {
        var repo = ConTres();

        var resultado = await repo.GetAsync(filtro: c => c.Nombre == "Comidas");

        resultado!.Id.Should().Be(2);
        repo.UltimoFiltro.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_con_un_filtro_sin_coincidencia_devuelve_null()
    {
        var repo = ConTres();

        (await repo.GetAsync(filtro: c => c.Nombre == "No existe")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_devuelve_una_copia_y_no_la_instancia_almacenada()
    {
        var repo = ConTres();
        var almacenada = repo.Entidades[0];

        var resultado = await repo.GetAsync(filtro: c => c.Id == 1);
        resultado!.Nombre = "Nombre cambiado en memoria";

        resultado.Should().NotBeSameAs(almacenada);
        almacenada.Nombre.Should().Be("Bebidas", "GetAsync equivale al AsNoTracking() de la linea 59");
    }

    [Fact]
    public async Task GetAsync_registra_el_include_recibido()
    {
        var repo = ConTres();

        await repo.GetAsync(filtro: c => c.Id == 1, incluir: "Producto");

        repo.UltimoInclude.Should().Be("Producto");
    }

    [Fact]
    public async Task GetAsync_con_el_gancho_AlGetAsync_tiene_prioridad_sobre_el_almacen()
    {
        var repo = ConTres();
        repo.AlGetAsync = _ => new CategoriaBuilder().ConId(555).ConNombre("Del gancho").Build();

        var resultado = await repo.GetAsync(filtro: c => c.Id == 1);

        resultado.Id.Should().Be(555);
        resultado.Nombre.Should().Be("Del gancho");
    }

    [Fact]
    public async Task GetAsync_con_el_gancho_AlGetAsync_permite_secuencias_de_reintentos()
    {
        var repo = new RepositorioGenericoVentasFake<Categoria>();
        var intentos = 0;
        repo.AlGetAsync = _ => ++intentos < 3 ? null : new CategoriaBuilder().ConId(9).ConNombre("Aparece al tercer intento").Build();

        (await repo.GetAsync()).Should().BeNull();
        (await repo.GetAsync()).Should().BeNull();
        (await repo.GetAsync()).Should().NotBeNull();
        repo.VecesGetAsync.Should().Be(3);
    }

    // ------------------------------------------------------------------ ListarAsync

    [Fact]
    public async Task ListarAsync_sin_filtro_ni_orden_devuelve_todas()
    {
        var repo = ConTres();

        var todas = await repo.ListarAsync();

        todas.Should().HaveCount(3);
        repo.VecesListar.Should().Be(1);
    }

    [Fact]
    public async Task ListarAsync_evalua_de_verdad_el_filtro()
    {
        var repo = ConTres();

        var filtradas = await repo.ListarAsync(filtro: c => c.Id >= 2);

        filtradas.Select(e => e.Nombre).Should().BeEquivalentTo(["Comidas", "Limpieza"]);
    }

    [Fact]
    public async Task ListarAsync_evalua_de_verdad_el_delegate_de_orden()
    {
        var repo = ConTres();

        var ordenadas = await repo.ListarAsync(orden: q => q.OrderByDescending(c => c.Id));

        ordenadas.Select(e => e.Id).Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task ListarAsync_combina_filtro_y_orden()
    {
        var repo = ConTres();

        var resultado = await repo.ListarAsync(
            filtro: c => c.Nombre.StartsWith("Co") || c.Nombre.StartsWith("Li"),
            orden: q => q.OrderBy(c => c.Nombre)
        );

        resultado.Select(e => e.Nombre).Should().Equal("Comidas", "Limpieza");
    }

    [Fact]
    public async Task ListarAsync_ordena_por_una_propiedad_numerable()
    {
        var repo = new RepositorioGenericoVentasFake<Producto>(
            new ProductoBuilder().ConId(1).ConPrecio(100m).Build(),
            new ProductoBuilder().ConId(2).ConPrecio(900m).Build(),
            new ProductoBuilder().ConId(3).ConPrecio(500m).Build()
        );

        var resultado = await repo.ListarAsync(orden: q => q.OrderByDescending(p => p.Precio));

        resultado.Select(p => p.Id).Should().Equal(2, 3, 1);
    }

    [Fact]
    public async Task ListarAsync_devuelve_las_instancias_reales_y_no_copias()
    {
        var repo = ConTres();
        var almacenada = repo.Entidades[0];

        var resultado = await repo.ListarAsync(filtro: c => c.Id == 1);
        resultado.Single().Nombre = "Mutado sobre la entidad trackeada";

        almacenada.Nombre.Should().Be("Mutado sobre la entidad trackeada");
    }

    [Fact]
    public async Task ListarAsync_registra_el_include_recibido()
    {
        var repo = ConTres();

        await repo.ListarAsync(incluir: "Producto,Venta");

        repo.UltimoInclude.Should().Be("Producto,Venta");
    }

    // ------------------------------------------------------------------ apoyo

    [Fact]
    public void Semear_agrega_entidades_al_almacen_existente()
    {
        var repo = ConTres();

        repo.Semear(new CategoriaBuilder().ConId(4).ConNombre("Bebidas Sin Alcohol").Build());

        repo.Entidades.Should().HaveCount(4);
    }

    [Fact]
    public async Task ListarAsync_registra_el_orden_que_se_pidio_en_la_ultima_consulta()
    {
        var repo = ConTres();

        await repo.ListarAsync(orden: q => q.OrderByDescending(c => c.Id));

        repo.OrdenEsperado().Select(e => e.Id).Should().Equal(3, 2, 1);
    }

    [Fact]
    public void OrdenEsperado_sin_una_consulta_previa_devuelve_una_lista_vacia()
    {
        new RepositorioGenericoVentasFake<Categoria>().OrdenEsperado().Should().BeEmpty();
    }

    [Fact]
    public async Task Limpiar_resetea_todos_los_registros()
    {
        var repo = ConTres();
        await repo.ListarAsync(filtro: c => c.Id > 0, orden: q => q.OrderBy(c => c.Id), incluir: "Producto");
        repo.ResultadosUpdate.Enqueue(0);

        repo.Limpiar();

        repo.Entidades.Should().BeEmpty();
        repo.VecesListar.Should().Be(0);
        repo.VecesGetAsync.Should().Be(0);
        repo.UltimoFiltro.Should().BeNull();
        repo.UltimoOrden.Should().BeNull();
        repo.UltimoInclude.Should().BeNull();
        repo.ResultadoUpdate.Should().Be(1);
        repo.ResultadosUpdate.Should().BeEmpty();
    }
}