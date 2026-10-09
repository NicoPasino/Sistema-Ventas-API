using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Modelos.Ventas;

namespace NicoPasino.Core.Mapper
{
    public static class MappingConfig
    {
        // TypeAdapterConfig es estado estatico global y reaplicar NewConfig() sobre
        // una config ya compilada lanza InvalidOperationException. Se protege para que
        // la inicializacion sea idempotente (el ensamblado de tests la carga una vez y
        // WebApplicationFactory vuelve a ejecutar Program.Main).
        private static readonly object Candado = new();
        private static bool _yaAplicado;

        public static void VentasMappings() {
            lock (Candado) {
                if (_yaAplicado) return;

                TypeAdapterConfig<Producto, ProductoDto>.NewConfig()
                    .TwoWays() // de modelo a dto / de dto a modelo.
                    .Map(dest => dest.Categoria, src => src.IdCategoriaNavigation.Nombre); // prop calculada

                TypeAdapterConfig<Venta, VentaDto>.NewConfig()
                    .TwoWays()
                    .Map(dest => dest.Cliente, src => src.IdClienteNavigation.Nombre)
                    .Map(dest => dest.Productos, src => src.Ventaporproducto);

                TypeAdapterConfig<Venta, VentaDetalleDto>.NewConfig()
                    .Map(dest => dest.Cliente, src => src.IdClienteNavigation)
                    .Map(dest => dest.Productos, src => src.Ventaporproducto)
                    .Map(dest => dest.Total, src => src.Ventaporproducto.Sum(x => x.SubTotal));

                TypeAdapterConfig<Ventaporproducto, VentaporproductoDto>.NewConfig()
                    .Map(dest => dest.Producto, src => src.NombreProducto ?? src.IdProductoNavigation!.Nombre);

                TypeAdapterConfig<Cliente, ClienteDto>.NewConfig()
                    .Map(dest => dest.NroCompras, src => src.Venta.Count());

                _yaAplicado = true;
            }
        }
    }
}
