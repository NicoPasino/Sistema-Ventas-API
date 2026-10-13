using FluentAssertions;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de la inyeccion de <see cref="TimeProvider"/> e <see cref="IGeneradorCodigos"/>
/// (#44): los timestamps dejan de leer el reloj del sistema y los codigos publicos
/// dejan de usar <c>Random</c> hardcodeado, asi que se pueden asertar de forma exacta.
/// </summary>
public class InyeccionTiempoYCodigosTests
{
    private static readonly DateTimeOffset FechaFija = new(2024, 6, 20, 15, 30, 0, TimeSpan.Zero);

    private static Categoria Bebidas => CategoriaBuilder.Uno().ConId(1).ConNombre("Bebidas").Build();

    private static Producto Producto(int id = 1, int idPublica = 1001, int cantidad = 50)
        => ProductoBuilder.Uno()
            .ConId(id)
            .ConIdPublica(idPublica)
            .ConNombre("Gaseosa 500ml")
            .ConPrecio(1500.50m)
            .ConCantidad(cantidad)
            .ConActivo(true)
            .ConCategoria(Bebidas)
            .Build();

    private static Cliente ClienteValido => ClienteBuilder.Uno()
        .ConId(1).ConDocumento(30111222).ConNombre("Ana Gomez").ConCorreo("ana@correo.com").Build();

    // ------------------------------------------------------------------ TimeProvider

    [Fact]
    public async Task Producto_Create_sella_las_fechas_con_el_reloj_inyectado()
    {
        var repo = new RepositorioGenericoVentasFake<Producto>();
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(Bebidas);
        var servicio = new ProductoServicio(repo, new ProductoValidador(repoCategoria), new RelojFijo(FechaFija));

        await servicio.Create(ProductoDtoBuilder.Uno().Build());

        var guardado = repo.Entidades.Single();
        guardado.FechaCreacion.Should().Be(FechaFija.UtcDateTime);
        guardado.FechaModificacion.Should().Be(FechaFija.UtcDateTime);
    }

    [Fact]
    public async Task Cliente_Create_sella_la_fecha_con_el_reloj_inyectado()
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>();
        var servicio = new ClienteServicio(repo, new ClienteValidador(repo), new RelojFijo(FechaFija));

        await servicio.Create(ClienteDtoBuilder.Uno().Build());

        repo.UltimaEntidadAgregada!.FechaCreacion.Should().Be(FechaFija.UtcDateTime);
    }

    [Fact]
    public async Task Venta_Create_usa_el_reloj_inyectado_cuando_el_DTO_no_trae_fecha()
    {
        var (servicio, repoVenta, _, _, _) = CrearVenta(new RelojFijo(FechaFija), new GeneradorCodigosFijo(555));

        await servicio.Create(VentaDtoBuilder.Uno().ConFechaVenta(null).Build());

        repoVenta.UltimaEntidadAgregada!.FechaVenta.Should().Be(FechaFija.UtcDateTime);
    }

    [Fact]
    public void ValidarFecha_compara_contra_el_reloj_inyectado()
    {
        var reloj = new RelojFijo(FechaFija);
        var validador = new VentaValidador(
            new RepositorioGenericoVentasFake<Cliente>(),
            new RepositorioGenericoVentasFake<Producto>(),
            reloj);

        validador.ValidarFecha(FechaFija.UtcDateTime);
        validador.Invoking(v => v.ValidarFecha(FechaFija.UtcDateTime.AddMinutes(1)))
            .Should().Throw<DataException>()
            .WithMessage("La fecha de la venta no puede ser futura.");
    }

    // ------------------------------------------------------------------ IGeneradorCodigos

    [Fact]
    public async Task Producto_Create_usa_el_generador_inyectado_para_el_IdPublica()
    {
        var repo = new RepositorioGenericoVentasFake<Producto>();
        var repoCategoria = new RepositorioGenericoVentasFake<Categoria>(Bebidas);
        var generador = new GeneradorCodigosFijo(777777);
        var servicio = new ProductoServicio(repo, new ProductoValidador(repoCategoria), generador: generador);

        await servicio.Create(ProductoDtoBuilder.Uno().ConIdPublica(null).Build());

        repo.UltimaEntidadAgregada!.IdPublica.Should().Be(777777);
        generador.Llamadas.Should().Be(1);
    }

    [Fact]
    public async Task Venta_Create_usa_el_generador_inyectado_para_el_Numero()
    {
        var (servicio, repoVenta, _, _, _) = CrearVenta(new RelojFijo(FechaFija), new GeneradorCodigosFijo(555));

        await servicio.Create(VentaDtoBuilder.Uno().Build());

        repoVenta.UltimaEntidadAgregada!.Numero.Should().Be(555);
    }

    [Fact]
    public async Task Venta_Create_reintenta_si_el_Numero_ya_existe()
    {
        var (servicio, repoVenta, _, _, _) = CrearVenta(
            new RelojFijo(FechaFija), new GeneradorCodigosFijo(111, 222));

        var llamadas = 0;
        repoVenta.AlGetAsync = _ => ++llamadas < 2 ? new Venta { Numero = 111 } : null;

        await servicio.Create(VentaDtoBuilder.Uno().Build());

        repoVenta.UltimaEntidadAgregada!.Numero.Should().Be(222, "111 ya existia");
        repoVenta.VecesGetAsync.Should().Be(2);
    }

    [Fact]
    public async Task Venta_Create_falla_si_tras_diez_intentos_el_Numero_sigue_ocupado()
    {
        var generador = new GeneradorCodigosFijo(111);
        var (servicio, repoVenta, _, _, _) = CrearVenta(new RelojFijo(FechaFija), generador);
        repoVenta.AlGetAsync = _ => new Venta { Numero = 111 };

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Create(VentaDtoBuilder.Uno().Build()));

        excepcion.Message.Should().Be("No se pudo generar un número único para la venta, reintentar.");
        generador.Llamadas.Should().Be(10);
    }

    [Fact]
    public void GeneradorCodigos_por_defecto_devuelve_valores_en_rango()
    {
        var generador = new GeneradorCodigos();

        for (int i = 0; i < 100; i++) {
            generador.Siguiente(1, 9999999).Should().BeInRange(1, 9999998);
        }
    }

    // ------------------------------------------------------------------ armado

    private static (VentaServicio Servicio, RepositorioGenericoVentasFake<Venta> RepoVenta,
        RepositorioGenericoVentasFake<Cliente> RepoCliente, RepositorioGenericoVentasFake<Producto> RepoProducto,
        RepositorioGenericoVentasFake<Ventaporproducto> RepoVpp)
        CrearVenta(TimeProvider reloj, GeneradorCodigosFijo generador)
    {
        var repoVenta = new RepositorioGenericoVentasFake<Venta>();
        var repoCliente = new RepositorioGenericoVentasFake<Cliente>(ClienteValido);
        var repoProducto = new RepositorioGenericoVentasFake<Producto>(Producto());
        var repoVpp = new RepositorioGenericoVentasFake<Ventaporproducto>();
        var validador = new VentaValidador(repoCliente, repoProducto, reloj);
        var servicio = new VentaServicio(repoVenta, repoCliente, repoProducto, repoVpp, validador, reloj, generador);
        return (servicio, repoVenta, repoCliente, repoProducto, repoVpp);
    }
}
