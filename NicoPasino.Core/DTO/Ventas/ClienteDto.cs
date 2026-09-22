using System.ComponentModel.DataAnnotations;

namespace NicoPasino.Core.DTO.Ventas;

public partial class ClienteDto
{
    [Required(ErrorMessage = "El nombre es requerido.")]
    [StringLength(100, MinimumLength = 4, ErrorMessage = "El nombre debe tener entre 4 y 100 caracteres.")]
    public string Nombre { get; set; }

    [Required(ErrorMessage = "El correo es requerido.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede tener más de 150 caracteres.")]
    public string Correo { get; set; }

    [Required(ErrorMessage = "El documento es requerido.")]
    [Range(10000000, 99999999, ErrorMessage = "El documento debe tener 8 dígitos.")]
    public int Documento { get; set; }

    public int? NroCompras { get; set; }

    public DateTime? FechaCreacion { get; set; }
}
