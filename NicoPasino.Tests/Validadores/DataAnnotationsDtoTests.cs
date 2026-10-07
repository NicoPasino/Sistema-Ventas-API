using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using NicoPasino.Core.DTO.Ventas;

namespace NicoPasino.Tests.Validadores;

/// <summary>
/// Tests de las DataAnnotations de los DTOs de entrada (#28). El model binding de
/// ASP.NET Core valida estas reglas <b>antes</b> de entrar al action, asi que son
/// la primera barrera de la API.
/// <para>
/// Se usa <see cref="Validator.TryValidateObject"/> con <c>validateAllProperties</c>
/// y se afirma el <c>ErrorMessage</c> textual. Cada caso parte de un DTO valido y
/// pisa un unico campo, de modo que el error esperado es el unico.
/// </para>
/// </summary>
public class DataAnnotationsDtoTests
{
    private static IReadOnlyList<string> Errores(object dto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
        return resultados.Select(r => r.ErrorMessage!).ToList();
    }

    // ------------------------------------------------------------------ fixtures

    private static ProductoDto ProductoValido() => new() {
        IdCategoria = 1,
        Nombre = "Gaseosa 500ml",
        Descripcion = "Botella de plástico",
        Cantidad = 10,
        Precio = 1500.50m,
        StockMinimo = 5,
        StockMaximo = 100,
        Proveedor = "Distribuidora Central"
    };

    private static VentaDto VentaValida() => new() {
        DNI = 30111222,
        Detalle = "Compra",
        ItemsId = [1],
        ItemsCant = [1]
    };

    private static VentaporproductoDto ItemValido() => new() {
        Producto = "Gaseosa 500ml",
        Cantidad = 2,
        PrecioUnitario = 1500.50m,
        SubTotal = 3001m
    };

    // ================================================================== ProductoDto

    [Fact]
    public void Producto_valido_no_tiene_errores()
        => Errores(ProductoValido()).Should().BeEmpty();

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100000000)]
    public void Producto_con_precio_fuera_de_rango_informa_el_mensaje(double precio)
    {
        var dto = ProductoValido();
        dto.Precio = (decimal)precio;

        Errores(dto).Should().Equal("El precio no puede ser negativo ni superar 99.999.999,99.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99999999.99)]
    public void Producto_con_precio_en_el_limite_no_tiene_errores(double precio)
    {
        var dto = ProductoValido();
        dto.Precio = (decimal)precio;

        Errores(dto).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Producto_con_id_categoria_invalido_informa_el_mensaje(int idCategoria)
    {
        var dto = ProductoValido();
        dto.IdCategoria = idCategoria;

        Errores(dto).Should().Equal("La categoría no es válida.");
    }

    [Fact]
    public void Producto_con_nombre_muy_corto_informa_el_mensaje()
    {
        var dto = ProductoValido();
        dto.Nombre = "A";

        Errores(dto).Should().Equal("El nombre debe tener entre 2 y 100 caracteres.");
    }

    [Fact]
    public void Producto_con_nombre_muy_largo_informa_el_mensaje()
    {
        var dto = ProductoValido();
        dto.Nombre = new string('a', 101);

        Errores(dto).Should().Equal("El nombre debe tener entre 2 y 100 caracteres.");
    }

    [Fact]
    public void Producto_con_descripcion_muy_larga_informa_el_mensaje()
    {
        var dto = ProductoValido();
        dto.Descripcion = new string('a', 501);

        Errores(dto).Should().Equal("La descripción no puede tener más de 500 caracteres.");
    }

    [Fact]
    public void Producto_con_cantidad_negativa_informa_el_mensaje()
    {
        var dto = ProductoValido();
        dto.Cantidad = -1;

        Errores(dto).Should().Equal("La cantidad no puede ser negativa.");
    }

    [Fact]
    public void Producto_con_stock_minimo_negativo_informa_el_mensaje()
    {
        var dto = ProductoValido();
        dto.StockMinimo = -1;

        Errores(dto).Should().Equal("El stock mínimo no puede ser negativo.");
    }

    [Fact]
    public void Producto_con_stock_maximo_negativo_informa_el_mensaje()
    {
        var dto = ProductoValido();
        dto.StockMaximo = -1;

        Errores(dto).Should().Equal("El stock máximo no puede ser negativo.");
    }

    [Fact]
    public void Producto_con_proveedor_muy_largo_informa_el_mensaje()
    {
        var dto = ProductoValido();
        dto.Proveedor = new string('a', 151);

        Errores(dto).Should().Equal("El proveedor no puede tener más de 150 caracteres.");
    }

    // ===================================================================== VentaDto

    [Fact]
    public void Venta_valida_no_tiene_errores()
        => Errores(VentaValida()).Should().BeEmpty();

    [Fact]
    public void Venta_sin_DNI_informa_que_es_requerido()
    {
        var dto = VentaValida();
        dto.DNI = null;

        Errores(dto).Should().Equal("El DNI es requerido.");
    }

    [Theory]
    [InlineData(9999999)]
    [InlineData(100000000)]
    public void Venta_con_DNI_de_longitud_incorrecta_informa_el_mensaje(int dni)
    {
        var dto = VentaValida();
        dto.DNI = dni;

        Errores(dto).Should().Equal("El DNI debe tener 8 dígitos.");
    }

    [Fact]
    public void Venta_sin_lista_de_productos_informa_que_es_requerida()
    {
        var dto = VentaValida();
        dto.ItemsId = null;

        Errores(dto).Should().Equal("La lista de productos es requerido.");
    }

    [Fact]
    public void Venta_con_lista_de_productos_vacia_informa_el_minimo()
    {
        var dto = VentaValida();
        dto.ItemsId = [];

        Errores(dto).Should().Equal("Debe haber al menos un producto.");
    }

    [Fact]
    public void Venta_sin_lista_de_cantidades_informa_que_es_requerida()
    {
        var dto = VentaValida();
        dto.ItemsCant = null;

        Errores(dto).Should().Equal("La lista de cantidades es requerido.");
    }

    [Fact]
    public void Venta_con_lista_de_cantidades_vacia_informa_el_minimo()
    {
        var dto = VentaValida();
        dto.ItemsCant = [];

        Errores(dto).Should().Equal("Debe haber al menos una cantidad.");
    }

    [Fact]
    public void Venta_con_detalle_muy_largo_informa_el_mensaje()
    {
        var dto = VentaValida();
        dto.Detalle = new string('a', 501);

        Errores(dto).Should().Equal("El detalle no puede tener más de 500 caracteres.");
    }

    // ============================================================= ClientePatchDto

    [Fact]
    public void ClientePatch_sin_campos_no_tiene_errores_porque_todo_es_opcional()
        => Errores(new ClientePatchDto()).Should().BeEmpty();

    [Fact]
    public void ClientePatch_con_nombre_corto_informa_el_mensaje()
    {
        var dto = new ClientePatchDto { Nombre = "A" };

        Errores(dto).Should().Equal("El nombre debe tener entre 4 y 100 caracteres.");
    }

    [Fact]
    public void ClientePatch_con_correo_invalido_informa_el_mensaje()
    {
        var dto = new ClientePatchDto { Correo = "sin-arroba" };

        Errores(dto).Should().Equal("El correo no tiene un formato válido.");
    }

    [Fact]
    public void ClientePatch_con_telefono_muy_largo_informa_el_mensaje()
    {
        var dto = new ClientePatchDto { Telefono = new string('1', 21) };

        Errores(dto).Should().Equal("El teléfono no puede tener más de 20 caracteres.");
    }

    // ========================================================= VentaporproductoDto

    [Fact]
    public void Item_valido_no_tiene_errores()
        => Errores(ItemValido()).Should().BeEmpty();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Item_con_cantidad_invalida_informa_el_mensaje(int cantidad)
    {
        var dto = ItemValido();
        dto.Cantidad = cantidad;

        Errores(dto).Should().Equal("La cantidad debe ser mayor a 0.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Item_con_precio_unitario_invalido_informa_el_mensaje(double precio)
    {
        var dto = ItemValido();
        dto.PrecioUnitario = (decimal)precio;

        Errores(dto).Should().Equal("El precio debe ser mayor a 0.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Item_con_subtotal_invalido_informa_el_mensaje(double subtotal)
    {
        var dto = ItemValido();
        dto.SubTotal = (decimal)subtotal;

        Errores(dto).Should().Equal("El subtotal debe ser mayor a 0.");
    }

    [Fact]
    public void Item_con_nombre_de_producto_muy_largo_informa_el_mensaje()
    {
        var dto = ItemValido();
        dto.Producto = new string('a', 256);

        Errores(dto).Should().Equal("Máximo 255 carácteres.");
    }
}
