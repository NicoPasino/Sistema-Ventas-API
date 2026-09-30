using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;

namespace NicoPasino.Servicios.Validaciones
{
    public class VentaValidador
    {
        public const int DocumentoMinimo = 10000000; // 8 dígitos
        public const int DocumentoMaximo = 99999999;
        public const int DetalleMaximo = 500;
        public const int CantidadMinima = 1;

        private readonly IRepositorioGenericoVentas<Cliente> _repoCliente;
        private readonly IRepositorioGenericoVentas<Producto> _repoProducto;

        public VentaValidador(IRepositorioGenericoVentas<Cliente> repoCliente,
                              IRepositorioGenericoVentas<Producto> repoProducto) {
            _repoCliente = repoCliente ?? throw new ArgumentNullException(nameof(repoCliente));
            _repoProducto = repoProducto ?? throw new ArgumentNullException(nameof(repoProducto));
        }

        public void NormalizarYValidar(VentaDto obj) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");

            ValidarDni(obj.DNI);

            if (obj.Detalle != null) {
                obj.Detalle = obj.Detalle.Trim();
                ValidarDetalle(obj.Detalle);
                obj.Detalle = string.IsNullOrEmpty(obj.Detalle) ? null : obj.Detalle;
            }

            ValidarFecha(obj.FechaVenta);

            // materializa las listas una sola vez y deja el dto listo para el servicio
            var (ids, cants) = ValidarItems(obj.ItemsId, obj.ItemsCant);
            obj.ItemsId = ids;
            obj.ItemsCant = cants;
        }

        public (int[] Ids, int[] Cants) ValidarItems(IEnumerable<int>? itemsId, IEnumerable<int>? itemsCant) {
            if (itemsId == null) throw new DataException("No se recibió la lista de productos.");
            if (itemsCant == null) throw new DataException("No se recibió la lista de cantidades.");

            var ids = itemsId.ToArray();
            var cants = itemsCant.ToArray();

            if (ids.Length != cants.Length)
                throw new DataException("La lista de productos no coincide con la lista de cantidades.");
            if (ids.Length == 0)
                throw new DataException("Debe haber al menos un producto en la venta.");

            for (int i = 0; i < ids.Length; i++) {
                ValidarIdProducto(ids[i]);
                ValidarCantidad(cants[i]);
            }

            return (ids, cants);
        }

        public void ValidarDni(int? dni) {
            if (!dni.HasValue) throw new DataException("No se recibió el DNI del cliente.");
            if (dni.Value < DocumentoMinimo || dni.Value > DocumentoMaximo)
                throw new DataException("El DNI debe tener 8 dígitos.");
        }

        public void ValidarDetalle(string? detalle) {
            if (detalle == null) return;
            if (detalle.Length > DetalleMaximo)
                throw new DataException("El detalle no puede tener más de 500 caracteres.");
        }

        public void ValidarFecha(DateTime? fechaVenta) {
            if (!fechaVenta.HasValue) return;
            if (fechaVenta.Value > DateTime.UtcNow)
                throw new DataException("La fecha de la venta no puede ser futura.");
        }

        public void ValidarIdProducto(int id) {
            if (id < 1) throw new DataException("El código del producto no es válido.");
        }

        public void ValidarCantidad(int cantidad) {
            if (cantidad < CantidadMinima) throw new DataException("La cantidad debe ser mayor a cero.");
        }

        public void ValidarIdOperacion(int id) {
            if (id <= 0) throw new DataException("Numero de venta no válido");
        }

        // Devuelve el Id (PK) del cliente a partir del DNI. Los ítems de venta referencian IdCliente.
        public async Task<int> ValidarClienteAsync(int? dni) {
            ValidarDni(dni);

            var cliente = await _repoCliente.GetAsync(filtro: c => c.Documento == dni);
            if (cliente == null)
                throw new DataException($"El cliente con el DNI '{dni}' no existe.");

            return cliente.Id;
        }

        // Valida existencia, estado y stock; devuelve los productos ya cargados
        // para que el servicio no vuelva a consultarlos.
        public async Task<List<Producto>> ValidarProductosAsync(int[] ids, int[] cants) {
            if (ids == null) throw new DataException("No se recibió la lista de productos.");
            if (cants == null) throw new DataException("No se recibió la lista de cantidades.");
            if (ids.Length != cants.Length)
                throw new DataException("La lista de productos no coincide con la lista de cantidades.");
            if (ids.Length == 0)
                throw new DataException("Debe haber al menos un producto en la venta.");

            var productosValidados = new List<Producto>(ids.Length);

            for (int i = 0; i < ids.Length; i++) {
                var idPublica = ids[i];
                var cantidad = cants[i];

                ValidarIdProducto(idPublica);
                ValidarCantidad(cantidad);

                var productoDb = await _repoProducto.GetAsync(filtro: p => p.IdPublica == idPublica);
                if (productoDb == null)
                    throw new DataException($"Producto no encontrado (código del producto: {idPublica}).");

                if (!productoDb.Activo)
                    throw new DataException($"El producto '{productoDb.Nombre}' está inactivo y no se puede vender.");

                if (cantidad > productoDb.Cantidad)
                    throw new DataException(
                        $"Stock insuficiente para '{productoDb.Nombre}', cantidad solicitada: '{cantidad}', disponible: '{productoDb.Cantidad}'.");

                productosValidados.Add(productoDb);
            }

            return productosValidados;
        }
    }
}
