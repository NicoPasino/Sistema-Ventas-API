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
/// Tests de <c>VentaServicio.Update</c> (#23). Los items y el cliente no son
/// editables: el metodo obliga a rehacer la venta si el DTO los trae.
/// </summary>
public class VentaServicioUpdateTests
{
    private static Cliente ClientePorDefecto => ClienteBuilder.Uno()
        .ConId(1).ConDocumento(30111222).ConNombre("Ana Gomez").ConCorreo("ana@correo.com").Build();

    private static Venta Original(string? detalle = "Compra", DateTime? fechaVenta = null) => VentaBuilder.Uno()
        .ConId(5)
        .ConNumero(100)
        .ConDetalle(detalle)
        .ConFechaVenta(fechaVenta ?? new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc))
        .ConCliente(ClienteBuilder.Uno()
            .ConId(1).ConDocumento(30111222).ConNombre("Ana Gomez").ConCorreo("ana@correo.com").Build())
        .ConItems(VentaporproductoBuilder.Uno().ConCantidad(3).ConPrecioUnitario(1500.50m, "Gaseosa 500ml").Build())
        .Build();

    private static (VentaServicio Servicio, RepositorioGenericoVentasFake<Venta> Repo) CrearCon(Venta[] ventas = null!)
    {
        var repo = new RepositorioGenericoVentasFake<Venta>(ventas ?? [Original()]);
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>(ClientePorDefecto);
        var repoProducto = new RepositorioGenericoVentasFake<Producto>();
        var repoVpp = new RepositorioGenericoVentasFake<Ventaporproducto>();
        var servicio = new VentaServicio(repo, repoCliente, repoProducto, repoVpp,
            new VentaValidador(repoCliente, repoProducto));
        return (servicio, repo);
    }

    private static VentaDto Dto(int? id = 5, string? detalle = "Compra corregida",
        DateTime? fechaVenta = null) => new() {
            Id = id,
            Detalle = detalle,
            FechaVenta = fechaVenta
        };

    // ------------------------------------------------------------------ items y cliente no editables

    [Fact]
    public async Task Update_rechaza_un_DTO_con_ItemsId()
    {
        var (servicio, repo) = CrearCon();
        var dto = Dto();
        dto.ItemsId = [1001];

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(dto));

        excepcion.Message.Should().Be("No se pueden modificar los productos de una venta existente.");
        repo.VecesGetAsync.Should().Be(0, "corta antes de buscar la venta");
    }

    [Fact]
    public async Task Update_rechaza_un_DTO_con_ItemsCant_aunque_lista_de_productos_venga_null()
    {
        var (servicio, _) = CrearCon();
        var dto = Dto();
        dto.ItemsId = null;
        dto.ItemsCant = [1];

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(dto));

        excepcion.Message.Should().Be("No se pueden modificar los productos de una venta existente.");
    }

    /// <summary>
    /// Caracteriza que el bloqueo de la linea 178 mira solo si la lista es
    /// <c>null</c>, no si esta vacia: un <c>ItemsId = []</c> tambien se rechaza. El
    /// cliente tiene que omitir el campo entero, no mandarlo vacio.
    /// </summary>
    [Fact]
    public async Task Update_rechaza_listas_vacias_porque_el_guard_solo_mira_el_null()
    {
        var (servicio, _) = CrearCon();
        var dto = Dto();
        dto.ItemsId = [];
        dto.ItemsCant = [];

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(dto));

        excepcion.Message.Should().Be("No se pueden modificar los productos de una venta existente.");
    }

    /// <summary>
    /// Caracteriza el caso inverso al de "Update sin cambios": mandar solo el
    /// <c>Id</c> (detalle <c>null</c>, fecha <c>null</c>) <b>si</b> escribe, porque
    /// la comparacion de la linea 191 es <c>null == "Compra"</c>, o sea distinta. La
    /// venta queda intacta pero se paga un UPDATE de ida y vuelta. Para no escribir
    /// hay que mandar el detalle con el valor exacto que ya tiene.
    /// </summary>
    [Fact]
    public async Task Update_solo_con_el_Id_escribe_aunque_no_cambie_nada()
    {
        var (servicio, repo) = CrearCon();

        (await servicio.Update(new VentaDto { Id = 5 })).Should().BeTrue();
        repo.VecesUpdate.Should().Be(1, "null != 'Compra', asi que no entra al atajo");
        repo.UltimaEntidadActualizada!.Detalle.Should().Be("Compra", "pero no cambia nada");
    }

    // ------------------------------------------------------------------ validaciones de entrada

    [Fact]
    public async Task Update_rechaza_un_DTO_null()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(null!));

        excepcion.Message.Should().Be("No se recibió ningún dato.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Update_rechaza_un_Numero_de_venta_invalido(int? id)
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(Dto(id)));

        excepcion.Message.Should().Be("Numero de venta no válido");
        repo.VecesGetAsync.Should().Be(0);
    }

    [Fact]
    public async Task Update_rechaza_un_detalle_de_mas_de_500_caracteres()
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(Dto(5, new string('a', 501))));

        excepcion.Message.Should().Be("El detalle no puede tener más de 500 caracteres.");
        repo.VecesGetAsync.Should().Be(0);
    }

    [Fact]
    public async Task Update_rechaza_una_fecha_futura()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Update(Dto(5, "Compra corregida", DateTime.UtcNow.AddDays(1))));

        excepcion.Message.Should().Be("La fecha de la venta no puede ser futura.");
    }

    [Fact]
    public async Task Update_falla_si_no_encuentra_la_venta()
    {
        var (servicio, _) = CrearCon([Original()]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(Dto(999)));

        excepcion.Message.Should().Be("Venta no encontrada.");
    }

    // ------------------------------------------------------------------ camino feliz

    [Fact]
    public async Task Update_persiste_el_nuevo_detalle()
    {
        var (servicio, repo) = CrearCon();

        (await servicio.Update(Dto())).Should().BeTrue();
        repo.UltimaEntidadActualizada!.Detalle.Should().Be("Compra corregida");
    }

    [Fact]
    public async Task Update_persiste_la_nueva_fecha()
    {
        var (servicio, repo) = CrearCon();
        var nuevaFecha = new DateTime(2024, 7, 1, 9, 0, 0, DateTimeKind.Utc);

        await servicio.Update(Dto(5, "Compra corregida", nuevaFecha));

        repo.UltimaEntidadActualizada!.FechaVenta.Should().Be(nuevaFecha);
    }

    [Fact]
    public async Task Update_normaliza_el_detalle_con_Trim()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto(5, "   Compra corregida   "));

        repo.UltimaEntidadActualizada!.Detalle.Should().Be("Compra corregida");
    }

    [Fact]
    public async Task Update_conserva_el_Numero_el_cliente_y_los_items()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto());

        var actualizada = repo.UltimaEntidadActualizada!;
        actualizada.Numero.Should().Be(100);
        actualizada.IdCliente.Should().Be(1);
        actualizada.Ventaporproducto.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_solo_el_detalle_cambiado_no_toca_la_fecha()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto(5, "Compra corregida", null));

        repo.UltimaEntidadActualizada!.FechaVenta.Should()
            .Be(new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc), "linea 197: solo si viene con valor");
    }

    // ------------------------------------------------------------------ sin cambios

    [Fact]
    public async Task Update_sin_cambios_devuelve_true_sin_tocar_la_base()
    {
        var (servicio, repo) = CrearCon();

        (await servicio.Update(Dto(5, "Compra", new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc))))
            .Should().BeTrue();
        repo.VecesUpdate.Should().Be(0, "linea 193: sale temprano");
    }

    [Fact]
    public async Task Update_solo_con_el_detalle_distinto_considera_que_hay_cambios()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto(5, "Compra corregida", new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc)));

        repo.VecesUpdate.Should().Be(1);
    }

    [Fact]
    public async Task Update_solo_con_la_fecha_distinta_considera_que_hay_cambios()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto(5, "Compra", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        repo.VecesUpdate.Should().Be(1);
    }

    [Fact]
    public async Task Update_detalle_distinto_sin_fecha_tambien_escribe()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto(5, "Compra corregida", null));

        repo.VecesUpdate.Should().Be(1);
        repo.UltimaEntidadActualizada!.Detalle.Should().Be("Compra corregida");
    }

    [Fact]
    public async Task Update_detalle_igual_sin_fecha_no_escribe()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto(5, "Compra", null));

        repo.VecesUpdate.Should().Be(0, "el null de la fecha no cuenta como cambio");
    }

    /// <summary>
    /// Caracteriza que el DTO puede no traer nada editable (Id solo) y aun asi el
    /// metodo devuelve <c>true</c>: si <c>Detalle</c> viene <c>null</c> y
    /// <c>FechaVenta</c> <c>null</c>, la comparacion de la linea 191 da "sin cambios"
    /// y sale sin escribir. Un PUT sin detalle ni fecha escribe sin necesidad.
    /// </summary>


    /// <summary>
    /// Caracteriza que no se puede <b>borrar</b> el detalle. Si el DTO trae el
    /// detalle vacio, la linea 184 lo convierte en <c>null</c>, el
    /// <c>if (obj.Detalle != null)</c> de la linea 196 lo saltea y el detalle viejo
    /// sobrevive. Mismo bug que <c>ClienteServicio.Patch</c> con el telefono.
    /// Referencia: bug #43.
    /// </summary>
    [Fact]
    public async Task Update_no_puede_borrar_el_detalle_enviando_los_espacios()
    {
        var (servicio, repo) = CrearCon();

        (await servicio.Update(Dto(5, "   "))).Should().BeTrue();
        repo.UltimaEntidadActualizada!.Detalle.Should().Be("Compra", "el guard ve null y no lo aplica");
    }

    [Fact]
    public async Task Update_no_puede_borrar_el_detalle_enviando_null()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(Dto(5, null, new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc)));

        repo.UltimaEntidadActualizada!.Detalle.Should().Be("Compra");
    }

    [Fact]
    public async Task Update_sin_detalle_en_la_venta_ni_en_el_DTO_detecta_el_cambio_de_fecha()
    {
        var (servicio, repo) = CrearCon([Original(detalle: null)]);

        await servicio.Update(Dto(5, null, new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc)));

        repo.VecesUpdate.Should().Be(1, "null == null, pero la fecha si cambio");
        repo.UltimaEntidadActualizada!.Detalle.Should().BeNull();
    }

    [Fact]
    public async Task Update_sin_detalle_en_ningun_lado_y_sin_fecha_no_escribe()
    {
        var (servicio, repo) = CrearCon([Original(detalle: null)]);

        (await servicio.Update(Dto(5, null, null))).Should().BeTrue();
        repo.VecesUpdate.Should().Be(0);
    }

    [Fact]
    public async Task Update_sin_detalle_en_ningun_lado_y_con_la_misma_fecha_no_escribe()
    {
        var (servicio, repo) = CrearCon([Original(detalle: null)]);

        (await servicio.Update(Dto(5, null, new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc))))
            .Should().BeTrue();
        repo.VecesUpdate.Should().Be(0, "las tres condiciones del atajo se cumplen");
    }

    [Fact]
    public async Task Update_lanza_UpdateException_si_la_base_no_afecta_ninguna_fila()
    {
        var (servicio, repo) = CrearCon();
        repo.ResultadoUpdate = 0;

        var excepcion = await Assert.ThrowsAsync<UpdateException>(() => servicio.Update(Dto()));

        excepcion.Message.Should().Be("No se pudo actualizar en la base de datos.");
    }
}

/// <summary>
/// Tests de <c>VentaServicio.Enable</c> (#23). La tabla <c>venta</c> no tiene columna
/// <c>activo</c>, asi que el metodo no esta implementado.
/// </summary>
public class VentaServicioEnableTests
{
    private static (VentaServicio Servicio, RepositorioGenericoVentasFake<Venta> Repo) CrearCon()
    {
        var repo = new RepositorioGenericoVentasFake<Venta>();
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>();
        var repoProducto = new RepositorioGenericoVentasFake<Producto>();
        var repoVpp = new RepositorioGenericoVentasFake<Ventaporproducto>();
        var servicio = new VentaServicio(repo, repoCliente, repoProducto, repoVpp,
            new VentaValidador(repoCliente, repoProducto));
        return (servicio, repo);
    }

    /// <summary>
    /// Caracteriza que la baja logica de ventas no esta implementada, aunque
    /// <c>ProductoServicio</c> y <c>ClienteServicio</c> si exponen el mismo
    /// <c>Enable</c>. La interfaz <c>IServicioGenerico</c> obliga a declarar el metodo, asi
    /// que la API expone un endpoint que siempre responde 500.
    /// </summary>
    [Fact]
    public async Task Enable_lanza_NotImplementedException_siempre()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(1, true));

        excepcion.Message.Should().Be(
            "La baja de ventas no está implementada: la tabla 'venta' no tiene columna 'activo'.");
    }

    [Fact]
    public async Task Enable_falla_igual_para_el_estado_true_que_para_el_false()
    {
        var (servicio, _) = CrearCon();

        await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(1, false));
    }

    [Fact]
    public async Task Enable_no_toca_la_base_ni_valida_el_id()
    {
        var (servicio, repo) = CrearCon();

        await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(0, true));

        repo.VecesGetAsync.Should().Be(0);
        repo.VecesUpdate.Should().Be(0);
        repo.VecesListar.Should().Be(0);
    }
}