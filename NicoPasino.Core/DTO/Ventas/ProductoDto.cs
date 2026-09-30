using System.ComponentModel.DataAnnotations;

namespace NicoPasino.Core.DTO.Ventas
{
    public class ProductoDto
    {
        public int? IdPublica { get; set; }

        [Required(ErrorMessage = "La categoría es requerida.")]
        [Range(1, int.MaxValue, ErrorMessage = "La categoría no es válida.")]
        public int IdCategoria { get; set; }

        [Required(ErrorMessage = "El nombre es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
        public string Nombre { get; set; }

        //[Required(ErrorMessage = "La descripción es requerida.")]
        [StringLength(500, ErrorMessage = "La descripción no puede tener más de 500 caracteres.")]
        public string Descripcion { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa.")]
        public int Cantidad { get; set; }

        [Range(0, 99999999.99, ErrorMessage = "El precio no puede ser negativo ni superar 99.999.999,99.")]
        public decimal Precio { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
        public int? StockMinimo { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock máximo no puede ser negativo.")]
        public int? StockMaximo { get; set; }

        [StringLength(150, ErrorMessage = "El proveedor no puede tener más de 150 caracteres.")]
        public string? Proveedor { get; set; }

        public DateTime? FechaCreacion { get; set; }

        public DateTime? FechaModificacion { get; set; }

        public bool Activo { get; set; }

        public string? Categoria { get; set; }
    }
}
