using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;

namespace NicoPasino.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("general")]
    public partial class VentasController : ControllerBase
    {
        private readonly IServicioGenerico<Producto, ProductoDto, ProductoDto> _productoServicio;
        private readonly IServicioGenerico<Venta, VentaDto, VentaDetalleDto> _ventaServicio;
        private readonly IServicioGenerico<Cliente, ClienteDto, ClienteDto> _clienteServicio;
        private readonly IServicioGenerico<Categoria, CategoriaDto, CategoriaDto> _categoriaServicio;
        private readonly ProductoServicio _productoServicioConcreto;
        private readonly ClienteServicio _clienteServicioConcreto;

        public VentasController(IServicioGenerico<Producto, ProductoDto, ProductoDto> pServicio,
                                IServicioGenerico<Venta, VentaDto, VentaDetalleDto> vServicio,
                                IServicioGenerico<Cliente, ClienteDto, ClienteDto> clienteServicio,
                                IServicioGenerico<Categoria, CategoriaDto, CategoriaDto> categoriaServicio,
                                ProductoServicio productoServicioConcreto,
                                ClienteServicio clienteServicioConcreto) {
            _productoServicio = pServicio;
            _ventaServicio = vServicio;
            _clienteServicio = clienteServicio;
            _categoriaServicio = categoriaServicio;
            _productoServicioConcreto = productoServicioConcreto;
            _clienteServicioConcreto = clienteServicioConcreto;
        }


        // Nota: (Métodos y Rutas en otros archivos).

        // Controllers/
        // └── (Ventas)
        //     ├── VentasController.cs
        //     ├── Ventas.Productos.cs + (categorías)
        //     └── Ventas.Clientes.cs
        //     └── Ventas.Ventas.cs
    }
}
