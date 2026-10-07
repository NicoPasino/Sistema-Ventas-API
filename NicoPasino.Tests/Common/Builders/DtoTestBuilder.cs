using NicoPasino.Core.DTO.Ventas;

namespace NicoPasino.Tests.Common.Builders;

/// <summary>
/// Builders de los DTOs de entrada de los tres validadores.
/// <para>
/// El <c>Build()</c> de cada uno ya pasa la validacion completa
/// (<c>NormalizarYValidar</c>), asi que un test que solo necesita "algo valido"
/// no tiene que escribir 8 campos. Para probar un caso invalido se pisa el campo.
/// </para>
/// <para>
/// Los <c>Build*Invalido()</c> son los que usan los tests de caracterizacion de
/// bugs: dejan el DTO en el estado que dispara la excepcion.
/// </para>
/// </summary>
public sealed class ClienteDtoBuilder
{
    private string _nombre = "Juan Perez";
    private string _correo = "juan.perez@example.com";
    private int _documento = 30111222;
    private string? _telefono = "3514445555";
    private bool _activo = true;

    public static ClienteDtoBuilder Uno() => new();

    public ClienteDtoBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public ClienteDtoBuilder ConCorreo(string correo) { _correo = correo; return this; }
    public ClienteDtoBuilder ConDocumento(int documento) { _documento = documento; return this; }
    public ClienteDtoBuilder ConTelefono(string? telefono) { _telefono = telefono; return this; }
    public ClienteDtoBuilder ConActivo(bool activo) { _activo = activo; return this; }

    public ClienteDto Build() => new() {
        Nombre = _nombre,
        Correo = _correo,
        Documento = _documento,
        Telefono = _telefono!,
        Activo = _activo
    };
}

public sealed class ProductoDtoBuilder
{
    private int? _idPublica = 1001;
    private int _idCategoria = 1;
    private string _nombre = "Gaseosa 500ml";
    private string? _descripcion = "Botella de plástico";
    private int _cantidad = 50;
    private decimal _precio = 1500.50m;
    private int? _stockMinimo = 5;
    private int? _stockMaximo = 100;
    private string? _proveedor = "Distribuidora Central";
    private bool _activo = true;

    public static ProductoDtoBuilder Uno() => new();

    public ProductoDtoBuilder ConIdPublica(int? idPublica) { _idPublica = idPublica; return this; }
    public ProductoDtoBuilder ConIdCategoria(int idCategoria) { _idCategoria = idCategoria; return this; }
    public ProductoDtoBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public ProductoDtoBuilder ConDescripcion(string? descripcion) { _descripcion = descripcion; return this; }
    public ProductoDtoBuilder ConCantidad(int cantidad) { _cantidad = cantidad; return this; }
    public ProductoDtoBuilder ConPrecio(decimal precio) { _precio = precio; return this; }
    public ProductoDtoBuilder ConStockMinimo(int? stockMinimo) { _stockMinimo = stockMinimo; return this; }
    public ProductoDtoBuilder ConStockMaximo(int? stockMaximo) { _stockMaximo = stockMaximo; return this; }
    public ProductoDtoBuilder ConProveedor(string? proveedor) { _proveedor = proveedor; return this; }

    public ProductoDto Build() => new() {
        IdPublica = _idPublica,
        IdCategoria = _idCategoria,
        Nombre = _nombre,
        Descripcion = _descripcion!,
        Cantidad = _cantidad,
        Precio = _precio,
        StockMinimo = _stockMinimo,
        StockMaximo = _stockMaximo,
        Proveedor = _proveedor,
        Activo = _activo
    };
}

public sealed class VentaDtoBuilder
{
    private int? _dni = 30111222;
    private int? _numero = 1;
    private string? _detalle = "Compra";
    private DateTime? _fechaVenta = new(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private int[] _itemsId = [1001];
    private int[] _itemsCant = [2];

    public static VentaDtoBuilder Uno() => new();

    public VentaDtoBuilder ConDni(int? dni) { _dni = dni; return this; }
    public VentaDtoBuilder ConDetalle(string? detalle) { _detalle = detalle; return this; }
    public VentaDtoBuilder ConFechaVenta(DateTime? fecha) { _fechaVenta = fecha; return this; }

    /// <summary>Producto + cantidad, para el caso feliz de un solo item.</summary>
    public VentaDtoBuilder ConItem(int id, int cantidad)
    {
        _itemsId = [id];
        _itemsCant = [cantidad];
        return this;
    }

    /// <summary>Sobrescribe las dos listas completas, para probar desalineaciones.</summary>
    public VentaDtoBuilder ConItems(int[] ids, int[] cants)
    {
        _itemsId = ids;
        _itemsCant = cants;
        return this;
    }

    public VentaDto Build() => new() {
        DNI = _dni,
        Numero = _numero,
        Detalle = _detalle,
        FechaVenta = _fechaVenta,
        ItemsId = _itemsId,
        ItemsCant = _itemsCant
    };
}