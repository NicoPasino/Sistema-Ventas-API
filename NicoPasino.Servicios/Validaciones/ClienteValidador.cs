using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using System.ComponentModel.DataAnnotations;

namespace NicoPasino.Servicios.Validaciones
{
    public class ClienteValidador
    {
        public const int DocumentoMinimo = 10000000; // 8 dígitos
        public const int DocumentoMaximo = 99999999;
        public const int NombreMinimo = 4;
        public const int NombreMaximo = 100;
        public const int CorreoMinimo = 5;
        public const int CorreoMaximo = 150;
        public const int TelefonoMaximo = 20;

        private readonly IRepositorioGenericoVentas<Cliente> _repoG;

        public ClienteValidador(IRepositorioGenericoVentas<Cliente> repoG) {
            _repoG = repoG ?? throw new ArgumentNullException(nameof(repoG));
        }

        public void NormalizarYValidar(ClienteDto obj) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");

            ValidarDocumento(obj.Documento);

            ValidarNombre(obj.Nombre);
            obj.Nombre = obj.Nombre.Trim();

            ValidarCorreo(obj.Correo);
            obj.Correo = obj.Correo.Trim();

            if (obj.Telefono != null) {
                obj.Telefono = obj.Telefono.Trim();
                ValidarTelefono(obj.Telefono);
                obj.Telefono = string.IsNullOrEmpty(obj.Telefono) ? null : obj.Telefono;
            }
        }

        public void NormalizarYValidarPatch(ClientePatchDto obj) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");

            if (obj.Nombre == null
                && obj.Correo == null
                && obj.Telefono == null
                && obj.Activo == null) throw new DataException("No se recibieron datos para actualizar.");

            if (obj.Nombre != null) {
                ValidarNombre(obj.Nombre);
                obj.Nombre = obj.Nombre.Trim();
            }

            if (obj.Correo != null) {
                ValidarCorreo(obj.Correo);
                obj.Correo = obj.Correo.Trim();
            }

            if (obj.Telefono != null) {
                obj.Telefono = obj.Telefono.Trim();
                ValidarTelefono(obj.Telefono);
                obj.Telefono = string.IsNullOrEmpty(obj.Telefono) ? null : obj.Telefono;
            }
        }

        // Unicidad: 'documento' y 'correo' tienen índice único en la BD.
        public async Task ValidarDuplicadosAsync(ClienteDto obj, int? idExcluir = null) {
            await ValidarDocumentoUnicoAsync(obj.Documento, idExcluir);
            await ValidarCorreoUnicoAsync(obj.Correo, idExcluir);
        }

        public async Task ValidarDocumentoUnicoAsync(int documento, int? idExcluir = null) {
            var existente = await _repoG.GetAsync(filtro: c =>
                c.Documento == documento
                && (idExcluir == null || c.Id != idExcluir));

            if (existente != null)
                throw new DataException($"Ya existe un cliente con el documento '{documento}'.");
        }

        public async Task ValidarCorreoUnicoAsync(string correo, int? idExcluir = null) {
            if (string.IsNullOrWhiteSpace(correo)) return;

            var correoNormalizado = correo.Trim();
            var existente = await _repoG.GetAsync(filtro: c =>
                c.Correo == correoNormalizado
                && (idExcluir == null || c.Id != idExcluir));

            if (existente != null)
                throw new DataException($"Ya existe un cliente con el correo '{correoNormalizado}'.");
        }

        public void ValidarDocumento(int documento) {
            if (documento < DocumentoMinimo || documento > DocumentoMaximo)
                throw new DataException("El documento debe tener 8 dígitos.");
        }

        public void ValidarNombre(string nombre) {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new DataException("El nombre es requerido.");
            if (nombre.Trim().Length < NombreMinimo || nombre.Trim().Length > NombreMaximo)
                throw new DataException("El nombre debe tener entre 4 y 100 caracteres.");
        }

        public void ValidarCorreo(string correo) {
            if (string.IsNullOrWhiteSpace(correo))
                throw new DataException("El correo es requerido.");

            var correoNormalizado = correo.Trim();
            if (correoNormalizado.Length < CorreoMinimo || correoNormalizado.Length > CorreoMaximo)
                throw new DataException("El correo debe tener entre 5 y 150 caracteres.");
            if (!new EmailAddressAttribute().IsValid(correoNormalizado))
                throw new DataException("El correo no tiene un formato válido.");
        }

        public void ValidarTelefono(string? telefono) {
            if (telefono == null) return;
            if (telefono.Length > TelefonoMaximo)
                throw new DataException("El teléfono no puede tener más de 20 caracteres.");
        }
    }
}
