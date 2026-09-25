using System.ComponentModel.DataAnnotations;

namespace NicoPasino.Core.DTO.Ventas
{
    public class ProductoPatchDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "La categoría no es válida.")]
        public int? IdCategoria { get; set; }

        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
        public string? Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede tener más de 500 caracteres.")]
        public string? Descripcion { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa.")]
        public int? Cantidad { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo.")]
        public decimal? Precio { get; set; }

        public bool? Activo { get; set; }
    }
}