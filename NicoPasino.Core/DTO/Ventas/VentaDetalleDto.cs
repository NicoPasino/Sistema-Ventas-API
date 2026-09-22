namespace NicoPasino.Core.DTO.Ventas
{
    public class VentaDetalleDto
    {
        public int? Numero { get; set; }

        public string? Detalle { get; set; }

        public DateTime? FechaVenta { get; set; }

        public ClienteDto? Cliente { get; set; }

        public IEnumerable<VentaporproductoDto>? Productos { get; set; }

        public decimal Total { get; set; }
    }
}