using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Modelos.Ventas;

namespace NicoPasino.Core.Mapper
{
    public static class MappingConfig
    {
        public static void VentasMappings() {
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
        }
    }
}
