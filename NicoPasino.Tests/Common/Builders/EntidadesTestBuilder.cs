using NicoPasino.Core.Modelos.Ventas;

namespace NicoPasino.Tests.Common.Builders;

/// <summary>
/// Builders de las entidades de EF Core.
/// <para>
/// Todos los <c>Build()</c> devuelven una entidad <b>valida por defecto</b>: los
/// tests solo tienen que sobreescribir el campo que les interesa. Si un test
/// necesita una entidad invalida, la construye a mano a proposito.
/// </para>
/// <para>
/// El <c>Id</c> por defecto es <b>0</b>, que es el estado de "todavia no
/// persistida" y deja que el fake le asigne la pk al guardarla. Los tests que
/// siembran datos en el fake pasan <c>ConId(...)</c> de forma explicita.
/// </para>
/// </summary>
public sealed class CategoriaBuilder
{
    private int _id = 0;
    private string _nombre = "Bebidas";

    public static CategoriaBuilder Uno() => new();

    public CategoriaBuilder ConId(int id) { _id = id; return this; }
    public CategoriaBuilder ConNombre(string nombre) { _nombre = nombre; return this; }

    public Categoria Build() => new() { Id = _id, Nombre = _nombre };
}

public sealed class ClienteBuilder
{
    private int _id = 0;
    private string _nombre = "Juan Perez";
    private string _correo = "juan.perez@example.com";
    private int _documento = 30111222;
    private string? _telefono = "3514445555";
    private bool _activo = true;
    private DateTime _fechaCreacion = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static ClienteBuilder Uno() => new();

    public ClienteBuilder ConId(int id) { _id = id; return this; }
    public ClienteBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public ClienteBuilder ConCorreo(string correo) { _correo = correo; return this; }
    public ClienteBuilder ConDocumento(int documento) { _documento = documento; return this; }
    public ClienteBuilder ConTelefono(string? telefono) { _telefono = telefono; return this; }
    public ClienteBuilder ConActivo(bool activo) { _activo = activo; return this; }

    /// <summary>Cablea la coleccion de ventas, que <c>MappingConfig</c> cuenta para <c>NroCompras</c>.</summary>
    public ClienteBuilder ConCompras(params Venta[] ventas)
    {
        _ventas = ventas;
        return this;
    }

    private Venta[] _ventas = [];

    public Cliente Build()
    {
        var cliente = new Cliente {
            Id = _id,
            Nombre = _nombre,
            Correo = _correo,
            Documento = _documento,
            Telefono = _telefono!,
            Activo = _activo,
            FechaCreacion = _fechaCreacion
        };
        foreach (var v in _ventas) {
            v.IdCliente = _id;
            v.IdClienteNavigation = cliente;
            cliente.Venta.Add(v);
        }
        return cliente;
    }
}

public sealed class ProductoBuilder
{
    private int _id = 0;
    private int? _idPublica = 1001;
    private string _nombre = "Gaseosa 500ml";
    private string? _descripcion = "Botella de plástico";
    private int _cantidad = 50;
    private decimal _precio = 1500.50m;
    private bool _activo = true;
    private int? _stockMinimo = 5;
    private int? _stockMaximo = 100;
    private string? _proveedor = "Distribuidora Central";
    private DateTime? _fechaCreacion = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime? _fechaModificacion = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private Categoria _categoria = new CategoriaBuilder().Build();

    public static ProductoBuilder Uno() => new();

    public ProductoBuilder ConId(int id) { _id = id; return this; }
    public ProductoBuilder ConIdPublica(int? idPublica) { _idPublica = idPublica; return this; }
    public ProductoBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public ProductoBuilder ConCantidad(int cantidad) { _cantidad = cantidad; return this; }
    public ProductoBuilder ConPrecio(decimal precio) { _precio = precio; return this; }
    public ProductoBuilder ConActivo(bool activo) { _activo = activo; return this; }
    public ProductoBuilder ConCategoria(Categoria categoria) { _categoria = categoria; return this; }

    /// <summary>
    /// <c>MappingConfig</c> linea 12 hace <c>src.IdCategoriaNavigation.Nombre</c>
    /// sin null-check, asi que la navegacion siempre queda cableada.
    /// </summary>
    public Producto Build() => new() {
        Id = _id,
        IdPublica = _idPublica,
        IdCategoria = _categoria.Id,
        IdCategoriaNavigation = _categoria,
        Nombre = _nombre,
        Descripcion = _descripcion!,
        Cantidad = _cantidad,
        Precio = _precio,
        Activo = _activo,
        StockMinimo = _stockMinimo,
        StockMaximo = _stockMaximo,
        Proveedor = _proveedor!,
        FechaCreacion = _fechaCreacion,
        FechaModificacion = _fechaModificacion
    };
}

public sealed class VentaBuilder
{
    private int _id = 0;
    private int _idCliente = 1;
    private int? _numero = 1;
    private string? _detalle = "Compra";
    private DateTime? _fechaVenta = new(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private Cliente? _cliente;
    private Ventaporproducto[] _items = [];

    public static VentaBuilder Uno() => new();

    public VentaBuilder ConId(int id) { _id = id; return this; }
    public VentaBuilder ConNumero(int? numero) { _numero = numero; return this; }
    public VentaBuilder ConFechaVenta(DateTime? fecha) { _fechaVenta = fecha; return this; }
    public VentaBuilder ConCliente(Cliente cliente) { _cliente = cliente; _idCliente = cliente.Id; return this; }

    /// <summary>
    /// <c>MappingConfig</c> linea 16 desreferencia <c>IdClienteNavigation.Nombre</c>,
    /// asi que sin este cliente el <c>Adapt</c> revienta.
    /// </summary>
    public VentaBuilder ConClientePorDefecto() { _cliente = new ClienteBuilder().ConId(_idCliente).Build(); return this; }

    public VentaBuilder ConItems(params Ventaporproducto[] items) { _items = items; return this; }

    public Venta Build()
    {
        var venta = new Venta {
            Id = _id,
            IdCliente = _idCliente,
            Numero = _numero,
            Detalle = _detalle,
            FechaVenta = _fechaVenta,
            IdClienteNavigation = _cliente ?? new ClienteBuilder().ConId(_idCliente).Build()
        };
        foreach (var item in _items) {
            item.IdVenta = _id;
            item.IdVentaNavigation = venta;
            venta.Ventaporproducto.Add(item);
        }
        return venta;
    }
}

public sealed class VentaporproductoBuilder
{
    private int _idProducto = 1;
    private int _cantidad = 2;
    private string _nombreProducto = "Gaseosa 500ml";
    private decimal _precioUnitario = 1500.50m;
    private Producto? _producto;

    public static VentaporproductoBuilder Uno() => new();

    public VentaporproductoBuilder ConProducto(Producto producto)
    {
        _producto = producto;
        _idProducto = producto.Id;
        _nombreProducto = producto.Nombre;
        _precioUnitario = producto.Precio;
        return this;
    }

    public VentaporproductoBuilder ConCantidad(int cantidad) { _cantidad = cantidad; return this; }

    public Ventaporproducto Build()
    {
        var producto = _producto ?? new ProductoBuilder().ConId(_idProducto).Build();
        return new Ventaporproducto {
            IdProducto = _idProducto,
            Cantidad = _cantidad,
            NombreProducto = _nombreProducto,
            PrecioUnitario = _precioUnitario,
            SubTotal = _precioUnitario * _cantidad,
            IdProductoNavigation = producto
        };
    }
}