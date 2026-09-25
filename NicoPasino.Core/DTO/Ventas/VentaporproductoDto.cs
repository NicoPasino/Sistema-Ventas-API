using System.ComponentModel.DataAnnotations;

namespace NicoPasino.Core.DTO.Ventas;

public partial class VentaporproductoDto
{
    //public int IdVenta { get; set; }

    //public int IdProducto { get; set; }

    [StringLength(255, ErrorMessage = "Máximo 255 carácteres.")]
    public string? Producto { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
    public int Cantidad { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0.")]
    public decimal PrecioUnitario { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El subtotal debe ser mayor a 0.")]
    public decimal SubTotal { get; set; }

    //public virtual Producto IdProductoNavigation { get; set; }

    //public virtual Venta IdVentaNavigation { get; set; }
}