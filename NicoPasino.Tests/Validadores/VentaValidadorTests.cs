using FluentAssertions;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Validadores;

/// <summary>
/// Tests de los validadores sincronos de <see cref="VentaValidador"/> (#12):
/// <c>NormalizarYValidar</c>, <c>ValidarItems</c> y los validadores sueltos
/// de DNI, detalle, fecha, id de producto, cantidad e id de operacion.
/// </summary>
public class VentaValidadorTests
{
    private static VentaValidador CrearValidador()
        => new(new RepositorioGenericoVentasFake<Cliente>(), new RepositorioGenericoVentasFake<Producto>());

    // ------------------------------------------------------- NormalizarYValidar

    [Fact]
    public void NormalizarYValidar_acepta_un_dto_valido()
    {
        CrearValidador().NormalizarYValidar(VentaDtoBuilder.Uno().Build());
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_dto_null()
    {
        CrearValidador().Invoking(v => v.NormalizarYValidar(null!))
            .Should().Throw<DataException>()
            .WithMessage("No se recibió ningún dato.");
    }

    [Fact]
    public void NormalizarYValidar_recorta_el_detalle()
    {
        var validador = CrearValidador();
        var dto = VentaDtoBuilder.Uno().ConDetalle("  Compra mayorista  ").Build();

        validador.NormalizarYValidar(dto);

        dto.Detalle.Should().Be("Compra mayorista");
    }

    [Fact]
    public void NormalizarYValidar_convierte_un_detalle_vacio_en_null()
    {
        var validador = CrearValidador();
        var dto = VentaDtoBuilder.Uno().ConDetalle("   ").Build();

        validador.NormalizarYValidar(dto);

        dto.Detalle.Should().BeNull("la linea 32 normaliza el vacio a null");
    }

    [Fact]
    public void NormalizarYValidar_materializa_las_listas_de_items_en_arrays()
    {
        var validador = CrearValidador();
        var dto = VentaDtoBuilder.Uno()
            .ConItems([1001, 1002, 1003], [1, 2, 3])
            .Build();

        validador.NormalizarYValidar(dto);

        dto.ItemsId.Should().BeOfType<int[]>();
        dto.ItemsCant.Should().BeOfType<int[]>();
        dto.ItemsId.Should().Equal(1001, 1002, 1003);
        dto.ItemsCant.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_detalle_de_mas_de_500_caracteres()
    {
        var validador = CrearValidador();
        var dto = VentaDtoBuilder.Uno().ConDetalle(new string('a', 501)).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El detalle no puede tener más de 500 caracteres.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_dni_invalido()
    {
        var validador = CrearValidador();
        var dto = VentaDtoBuilder.Uno().ConDni(123).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El DNI debe tener 8 dígitos.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_una_fecha_futura()
    {
        var validador = CrearValidador();
        var dto = VentaDtoBuilder.Uno().ConFechaVenta(DateTime.UtcNow.AddDays(1)).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("La fecha de la venta no puede ser futura.");
    }

    // ------------------------------------------------------- ValidarItems

    [Fact]
    public void ValidarItems_acepta_las_listas_coincidentes()
    {
        var (ids, cants) = CrearValidador().ValidarItems([1001, 1002], [1, 5]);

        ids.Should().Equal(1001, 1002);
        cants.Should().Equal(1, 5);
    }

    [Fact]
    public void ValidarItems_rechaza_una_lista_de_ids_null()
    {
        CrearValidador().Invoking(v => v.ValidarItems(null, [1]))
            .Should().Throw<DataException>()
            .WithMessage("No se recibió la lista de productos.");
    }

    [Fact]
    public void ValidarItems_rechaza_una_lista_de_cantidades_null()
    {
        CrearValidador().Invoking(v => v.ValidarItems([1001], null))
            .Should().Throw<DataException>()
            .WithMessage("No se recibió la lista de cantidades.");
    }

    [Fact]
    public void ValidarItems_rechaza_listas_de_distinto_largo()
    {
        CrearValidador().Invoking(v => v.ValidarItems([1001, 1002], [1]))
            .Should().Throw<DataException>()
            .WithMessage("La lista de productos no coincide con la lista de cantidades.");
    }

    [Fact]
    public void ValidarItems_rechaza_las_dos_listas_vacias()
    {
        CrearValidador().Invoking(v => v.ValidarItems([], []))
            .Should().Throw<DataException>()
            .WithMessage("Debe haber al menos un producto en la venta.");
    }

    [Fact]
    public void ValidarItems_rechaza_un_id_de_producto_invalido()
    {
        CrearValidador().Invoking(v => v.ValidarItems([0], [1]))
            .Should().Throw<DataException>()
            .WithMessage("El código del producto no es válido.");
    }

    [Fact]
    public void ValidarItems_rechaza_una_cantidad_de_cero()
    {
        CrearValidador().Invoking(v => v.ValidarItems([1001], [0]))
            .Should().Throw<DataException>()
            .WithMessage("La cantidad debe ser mayor a cero.");
    }

    [Fact]
    public void ValidarItems_valida_en_orden_y_se_detiene_en_el_primer_error()
    {
        // El id es invalido y la cantidad tambien: el id se valida primero (linea 56).
        CrearValidador().Invoking(v => v.ValidarItems([-1], [-1]))
            .Should().Throw<DataException>()
            .WithMessage("El código del producto no es válido.");
    }

    [Fact]
    public void ValidarItems_materializa_a_arrays_aunque_le_lleguen_Lists()
    {
        var ids = new List<int> { 1001 };
        var cants = new List<int> { 3 };

        var resultado = CrearValidador().ValidarItems(ids, cants);

        resultado.Ids.Should().BeOfType<int[]>();
        resultado.Cants.Should().BeOfType<int[]>();
    }

    // ------------------------------------------------------- ValidarDni

    [Fact]
    public void ValidarDni_rechaza_un_dni_null()
    {
        CrearValidador().Invoking(v => v.ValidarDni(null))
            .Should().Throw<DataException>()
            .WithMessage("No se recibió el DNI del cliente.");
    }

    [Theory]
    [InlineData(VentaValidador.DocumentoMinimo)]
    [InlineData(30111222)]
    [InlineData(VentaValidador.DocumentoMaximo)]
    public void ValidarDni_acepta_valores_de_8_digitos(int dni)
    {
        CrearValidador().ValidarDni(dni);
    }

    [Theory]
    [InlineData(VentaValidador.DocumentoMinimo - 1)]
    [InlineData(0)]
    [InlineData(VentaValidador.DocumentoMaximo + 1)]
    public void ValidarDni_rechaza_valores_fuera_del_rango_de_8_digitos(int dni)
    {
        CrearValidador().Invoking(v => v.ValidarDni(dni))
            .Should().Throw<DataException>()
            .WithMessage("El DNI debe tener 8 dígitos.");
    }

    // ------------------------------------------------------- ValidarDetalle

    [Fact]
    public void ValidarDetalle_acepta_null()
    {
        CrearValidador().ValidarDetalle(null);
    }

    [Fact]
    public void ValidarDetalle_acepta_exactamente_500_caracteres()
    {
        CrearValidador().ValidarDetalle(new string('a', 500));
    }

    [Fact]
    public void ValidarDetalle_rechaza_501_caracteres()
    {
        CrearValidador().Invoking(v => v.ValidarDetalle(new string('a', 501)))
            .Should().Throw<DataException>()
            .WithMessage("El detalle no puede tener más de 500 caracteres.");
    }

    // ------------------------------------------------------- ValidarFecha

    [Fact]
    public void ValidarFecha_acepta_null()
    {
        CrearValidador().ValidarFecha(null);
    }

    [Fact]
    public void ValidarFecha_acepta_el_ahora()
    {
        CrearValidador().ValidarFecha(DateTime.UtcNow);
    }

    [Fact]
    public void ValidarFecha_acepta_el_pasado()
    {
        CrearValidador().ValidarFecha(DateTime.UtcNow.AddDays(-30));
    }

    [Fact]
    public void ValidarFecha_rechaza_el_futuro()
    {
        CrearValidador().Invoking(v => v.ValidarFecha(DateTime.UtcNow.AddMinutes(1)))
            .Should().Throw<DataException>()
            .WithMessage("La fecha de la venta no puede ser futura.");
    }

    /// <summary>
    /// Caracteriza que la comparacion es contra <c>DateTime.UtcNow</c> y no contra
    /// <c>DateTime.Now</c>: una fecha "hoy" a la manana segun la hora local puede
    /// caer en el pasado o en el futuro segun el reloj de UTC.
    /// Referencia: #12.
    /// </summary>
    [Fact]
    public void ValidarFecha_compara_contra_el_reloj_de_UTC()
    {
        var dentroDeUnRato = DateTime.UtcNow.AddHours(2);

        CrearValidador().Invoking(v => v.ValidarFecha(dentroDeUnRato))
            .Should().Throw<DataException>();
    }

    // ------------------------------------------------------- ValidarIdProducto / Cantidad

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarIdProducto_rechaza_ids_no_positivos(int id)
    {
        CrearValidador().Invoking(v => v.ValidarIdProducto(id))
            .Should().Throw<DataException>()
            .WithMessage("El código del producto no es válido.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1001)]
    public void ValidarIdProducto_acepta_ids_positivos(int id)
    {
        CrearValidador().ValidarIdProducto(id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ValidarCantidad_rechaza_valores_no_positivos(int cantidad)
    {
        CrearValidador().Invoking(v => v.ValidarCantidad(cantidad))
            .Should().Throw<DataException>()
            .WithMessage("La cantidad debe ser mayor a cero.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void ValidarCantidad_acepta_el_minimo_y_valores_positivos(int cantidad)
    {
        CrearValidador().ValidarCantidad(cantidad);
    }

    // ------------------------------------------------------- ValidarIdOperacion

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarIdOperacion_rechaza_numeros_no_positivos(int id)
    {
        CrearValidador().Invoking(v => v.ValidarIdOperacion(id))
            .Should().Throw<DataException>()
            .WithMessage("Numero de venta no válido");
    }

    [Fact]
    public void ValidarIdOperacion_acepta_un_numero_positivo()
    {
        CrearValidador().ValidarIdOperacion(1);
    }

    // ------------------------------------------------------- constructor

    [Fact]
    public void El_constructor_rechaza_un_repositorio_de_clientes_null()
    {
        Assert.Throws<ArgumentNullException>(() => new VentaValidador(null!, new RepositorioGenericoVentasFake<Producto>()));
    }

    [Fact]
    public void El_constructor_rechaza_un_repositorio_de_productos_null()
    {
        Assert.Throws<ArgumentNullException>(() => new VentaValidador(new RepositorioGenericoVentasFake<Cliente>(), null!));
    }
}