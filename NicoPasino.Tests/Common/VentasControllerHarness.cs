using NSubstitute;
using NicoPasino.Controllers;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Common;

/// <summary>
/// Arma un <see cref="VentasController"/> para los tests de endpoint (#26, #27, #28).
/// <para>
/// Las cuatro dependencias de tipo <see cref="IServicioGenerico{TEntity,TEntradaDto,TSalidaDto}"/>
/// se reemplazan por sustitutos de NSubstitute: los endpoints GET/POST/PUT/DELETE
/// solo orquestan (traducen resultado a codigo HTTP), asi que se prueba esa
/// traduccion sin arrastrar el servicio real.
/// </para>
/// <para>
/// <b>Por que hay servicios concretos.</b> <c>VentasController</c> tambien recibe
/// <see cref="ProductoServicio"/> y <see cref="ClienteServicio"/> como clases
/// concretas, porque los endpoints PATCH usan metodos no-virtuales (<c>Patch</c>)
/// que no se pueden sustituir. Por eso el harness expone <see cref="ClienteConcreto"/>
/// cableado a <see cref="RepoCliente"/>, para ejercitar el PATCH de verdad.
/// </para>
/// </summary>
public sealed class VentasControllerHarness
{
    public IServicioGenerico<Producto, ProductoDto, ProductoDto> ServicioProducto { get; }
    public IServicioGenerico<Venta, VentaDto, VentaDetalleDto> ServicioVenta { get; }
    public IServicioGenerico<Cliente, ClienteDto, ClienteDto> ServicioCliente { get; }
    public IServicioGenerico<Categoria, CategoriaDto, CategoriaDto> ServicioCategoria { get; }

    public RepositorioGenericoVentasFake<Producto> RepoProducto { get; }
    public RepositorioGenericoVentasFake<Cliente> RepoCliente { get; }
    public RepositorioGenericoVentasFake<Categoria> RepoCategoria { get; }

    public ProductoServicio ProductoConcreto { get; }
    public ClienteServicio ClienteConcreto { get; }

    public VentasController Controller { get; }

    public VentasControllerHarness(
        Producto[]? productos = null,
        Cliente[]? clientes = null,
        Categoria[]? categorias = null)
    {
        RepoCategoria = new RepositorioGenericoVentasFake<Categoria>(categorias ?? []);
        RepoCliente = new RepositorioGenericoVentasFake<Cliente>(clientes ?? []);
        RepoProducto = new RepositorioGenericoVentasFake<Producto>(productos ?? []);

        ProductoConcreto = new ProductoServicio(RepoProducto, new ProductoValidador(RepoCategoria));
        ClienteConcreto = new ClienteServicio(RepoCliente, new ClienteValidador(RepoCliente));

        ServicioProducto = Substitute.For<IServicioGenerico<Producto, ProductoDto, ProductoDto>>();
        ServicioVenta = Substitute.For<IServicioGenerico<Venta, VentaDto, VentaDetalleDto>>();
        ServicioCliente = Substitute.For<IServicioGenerico<Cliente, ClienteDto, ClienteDto>>();
        ServicioCategoria = Substitute.For<IServicioGenerico<Categoria, CategoriaDto, CategoriaDto>>();

        Controller = new VentasController(
            ServicioProducto,
            ServicioVenta,
            ServicioCliente,
            ServicioCategoria,
            ProductoConcreto,
            ClienteConcreto);
    }

    // ------------------------------------------------------------- clientes

    /// <summary>Siembra un cliente valido y devuelve la entidad para reusar su documento.</summary>
    public Cliente SembrarCliente(ClienteBuilder? personalizado = null)
    {
        var cliente = (personalizado ?? ClienteBuilder.Uno()).Build();
        if (cliente.Id == 0) cliente.Id = RepoCliente.Entidades.Count + 1;
        RepoCliente.Semear(cliente);
        return cliente;
    }
}
