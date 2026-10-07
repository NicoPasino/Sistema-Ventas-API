using FluentAssertions;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Validadores;

/// <summary>
/// Tests de los validadores sincronos de <see cref="ProductoValidador"/> (#8).
/// Cada test afirma el <b>mensaje exacto</b> de la excepcion: los controllers
/// devuelven esos mensajes al cliente, asi que cambiarlos es un breaking change
/// de la API.
/// </summary>
public class ProductoValidadorTests
{
    private static ProductoValidador CrearValidador()
        => new(new RepositorioGenericoVentasFake<Categoria>());

    // ------------------------------------------------------- NormalizarYValidar

    [Fact]
    public void NormalizarYValidar_acepta_un_dto_valido_y_no_lo_modifica()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().Build();

        validador.NormalizarYValidar(dto);

        dto.Nombre.Should().Be("Gaseosa 500ml");
        dto.Descripcion.Should().Be("Botella de plástico");
        dto.Proveedor.Should().Be("Distribuidora Central");
    }

    [Fact]
    public void NormalizarYValidar_recorta_los_espacios_de_nombre_descripcion_y_proveedor()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno()
            .ConNombre("  Gaseosa 500ml  ")
            .ConDescripcion("  Botella de plástico  ")
            .ConProveedor("  Distribuidora Central  ")
            .Build();

        validador.NormalizarYValidar(dto);

        dto.Nombre.Should().Be("Gaseosa 500ml");
        dto.Descripcion.Should().Be("Botella de plástico");
        dto.Proveedor.Should().Be("Distribuidora Central");
    }

    [Fact]
    public void NormalizarYValidar_convierte_la_descripcion_en_null_si_queda_vacia()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConDescripcion("   ").Build();

        validador.NormalizarYValidar(dto);

        dto.Descripcion.Should().BeNull("la linea 29 normaliza el vacio a null");
    }

    [Fact]
    public void NormalizarYValidar_convierte_el_proveedor_en_null_si_queda_vacio()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConProveedor("  ").Build();

        validador.NormalizarYValidar(dto);

        dto.Proveedor.Should().BeNull("la linea 42 normaliza el vacio a null");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NormalizarYValidar_rechaza_una_categoria_invalida(int idCategoria)
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConIdCategoria(idCategoria).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("La categoría no es válida.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_nombre_vacio()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConNombre("").Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El nombre es requerido.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_descripcion_de_mas_de_500_caracteres()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConDescripcion(new string('a', 501)).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("La descripción no puede tener más de 500 caracteres.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_precio_negativo()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConPrecio(-0.01m).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El precio no puede ser negativo.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_precio_que_supera_el_maximo_de_la_base()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConPrecio(ProductoValidador.PrecioMaximo + 0.01m).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El precio supera el máximo permitido (99.999.999,99).");
    }

    [Fact]
    public void NormalizarYValidar_acepta_el_precio_maximo_exacto_de_la_base()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConPrecio(ProductoValidador.PrecioMaximo).Build();

        validador.NormalizarYValidar(dto);
        dto.Precio.Should().Be(99_999_999.99m);
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_stock_minimo_mayor_al_maximo()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConStockMinimo(80).ConStockMaximo(20).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El stock mínimo no puede ser mayor al stock máximo.");
    }

    [Fact]
    public void NormalizarYValidar_acepta_un_stock_negativo_cuando_ambos_son_null()
    {
        var validador = CrearValidador();
        var dto = ProductoDtoBuilder.Uno().ConStockMinimo(null).ConStockMaximo(null).Build();

        validador.NormalizarYValidar(dto);
    }

    // ------------------------------------------------------- ValidarNombre

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    [InlineData("\t")]
    public void ValidarNombre_rechaza_un_nombre_vacio_o_solo_espacios(string nombre)
    {
        var validador = CrearValidador();

        var excepcion = Assert.Throws<DataException>(() => validador.ValidarNombre(nombre));

        excepcion.Message.Should().Be("El nombre es requerido.");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("A ")]
    public void ValidarNombre_rechaza_un_nombre_de_un_solo_caracter_por_longitud(string nombre)
    {
        var validador = CrearValidador();

        // No es whitespace, asi que cae en el chequeo de longitud (linea 60),
        // no en el de "nombre requerido".
        Assert.Throws<DataException>(() => validador.ValidarNombre(nombre))
            .Message.Should().Be("El nombre debe tener entre 2 y 100 caracteres.");
    }

    [Theory]
    [InlineData("Ab")]
    [InlineData(" Ab ")]
    public void ValidarNombre_mide_la_longitud_sobre_el_valor_ya_recortado(string nombre)
    {
        CrearValidador().ValidarNombre(nombre);
    }

    [Fact]
    public void ValidarNombre_acepta_exactamente_2_caracteres()
    {
        CrearValidador().ValidarNombre("Ag");
    }

    [Fact]
    public void ValidarNombre_rechaza_un_nombre_de_mas_de_100_caracteres()
    {
        CrearValidador().Invoking(v => v.ValidarNombre(new string('a', 101)))
            .Should().Throw<DataException>()
            .WithMessage("El nombre debe tener entre 2 y 100 caracteres.");
    }

    [Fact]
    public void ValidarNombre_acepta_exactamente_100_caracteres()
    {
        CrearValidador().ValidarNombre(new string('a', 100));
    }

    // ------------------------------------------------------- ValidarDescripcion

    [Fact]
    public void ValidarDescripcion_acepta_exactamente_500_caracteres()
    {
        CrearValidador().ValidarDescripcion(new string('a', 500));
    }

    [Fact]
    public void ValidarDescripcion_rechaza_501_caracteres()
    {
        CrearValidador().Invoking(v => v.ValidarDescripcion(new string('a', 501)))
            .Should().Throw<DataException>()
            .WithMessage("La descripción no puede tener más de 500 caracteres.");
    }

    // ------------------------------------------------------- ValidarCantidad / Precio / Stock

    [Fact]
    public void ValidarCantidad_rechaza_una_cantidad_negativa()
    {
        CrearValidador().Invoking(v => v.ValidarCantidad(-1))
            .Should().Throw<DataException>()
            .WithMessage("La cantidad no puede ser negativa.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ValidarCantidad_acepta_cero_y_positivos(int cantidad)
    {
        CrearValidador().ValidarCantidad(cantidad);
    }

    [Fact]
    public void ValidarPrecio_rechaza_un_precio_negativo()
    {
        CrearValidador().Invoking(v => v.ValidarPrecio(-0.01m))
            .Should().Throw<DataException>()
            .WithMessage("El precio no puede ser negativo.");
    }

    [Fact]
    public void ValidarPrecio_acepta_cero()
    {
        CrearValidador().ValidarPrecio(0m);
    }

    [Fact]
    public void ValidarStock_rechaza_un_stock_negativo()
    {
        CrearValidador().Invoking(v => v.ValidarStock(-1))
            .Should().Throw<DataException>()
            .WithMessage("El stock no puede ser negativo.");
    }

    [Fact]
    public void ValidarProveedor_rechaza_un_proveedor_de_mas_de_150_caracteres()
    {
        CrearValidador().Invoking(v => v.ValidarProveedor(new string('a', 151)))
            .Should().Throw<DataException>()
            .WithMessage("El proveedor no puede tener más de 150 caracteres.");
    }

    [Fact]
    public void ValidarProveedor_acepta_exactamente_150_caracteres()
    {
        CrearValidador().ValidarProveedor(new string('a', 150));
    }

    // ------------------------------------------------------- ValidarStockMinMax

    [Theory]
    [InlineData(10, 10)]
    [InlineData(5, 10)]
    public void ValidarStockMinMax_acepta_cuando_el_minimo_no_supera_al_maximo(int min, int max)
    {
        CrearValidador().ValidarStockMinMax(min, max);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(10, null)]
    [InlineData(null, null)]
    public void ValidarStockMinMax_acepta_cuando_alguno_es_null(int? min, int? max)
    {
        CrearValidador().ValidarStockMinMax(min, max);
    }

    [Fact]
    public void ValidarStockMinMax_rechaza_minimo_mayor_al_maximo()
    {
        CrearValidador().Invoking(v => v.ValidarStockMinMax(20, 10))
            .Should().Throw<DataException>()
            .WithMessage("El stock mínimo no puede ser mayor al stock máximo.");
    }

    // ------------------------------------------------------- Null y constructor

    [Fact]
    public void NormalizarYValidar_rechaza_un_dto_null()
    {
        CrearValidador().Invoking(v => v.NormalizarYValidar(null!))
            .Should().Throw<DataException>()
            .WithMessage("No se recibió ningún dato.");
    }

    [Fact]
    public void El_constructor_rechaza_un_repositorio_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ProductoValidador(null!));
    }
}