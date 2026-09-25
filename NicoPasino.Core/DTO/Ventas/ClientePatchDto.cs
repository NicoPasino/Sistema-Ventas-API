using System.ComponentModel.DataAnnotations;

namespace NicoPasino.Core.DTO.Ventas
{
    public class ClientePatchDto
    {
        [StringLength(100, MinimumLength = 4, ErrorMessage = "El nombre debe tener entre 4 y 100 caracteres.")]
        public string? Nombre { get; set; }

        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        [StringLength(150, ErrorMessage = "El correo no puede tener más de 150 caracteres.")]
        public string? Correo { get; set; }

        [StringLength(20, ErrorMessage = "El teléfono no puede tener más de 20 caracteres.")]
        public string? Telefono { get; set; }

        public bool? Activo { get; set; }
    }
}