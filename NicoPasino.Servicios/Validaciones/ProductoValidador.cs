using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;

namespace NicoPasino.Servicios.Validaciones
{
    public class ProductoValidador
    {
        public const decimal PrecioMaximo = 99999999.99m; // límite de decimal(10,2) en la BD

        private readonly IRepositorioGenericoVentas<Categoria> _repoCategoria;

        public ProductoValidador(IRepositorioGenericoVentas<Categoria> repoCategoria) {
            _repoCategoria = repoCategoria ?? throw new ArgumentNullException(nameof(repoCategoria));
        }

        public void NormalizarYValidar(ProductoDto obj) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");

            if (obj.IdCategoria < 1) throw new DataException("La categoría no es válida.");

            ValidarNombre(obj.Nombre);
            obj.Nombre = obj.Nombre.Trim();

            if (obj.Descripcion != null) {
                obj.Descripcion = obj.Descripcion.Trim();
                ValidarDescripcion(obj.Descripcion);
                obj.Descripcion = string.IsNullOrEmpty(obj.Descripcion) ? null : obj.Descripcion;
            }

            ValidarCantidad(obj.Cantidad);
            ValidarPrecio(obj.Precio);

            if (obj.StockMinimo.HasValue) ValidarStock(obj.StockMinimo.Value);
            if (obj.StockMaximo.HasValue) ValidarStock(obj.StockMaximo.Value);
            ValidarStockMinMax(obj.StockMinimo, obj.StockMaximo);

            if (obj.Proveedor != null) {
                obj.Proveedor = obj.Proveedor.Trim();
                ValidarProveedor(obj.Proveedor);
                obj.Proveedor = string.IsNullOrEmpty(obj.Proveedor) ? null : obj.Proveedor;
            }
        }

        public async Task ValidarCategoriaAsync(int idCategoria) {
            var categoria = await _repoCategoria.GetAsync(filtro: c => c.Id == idCategoria);
            if (categoria == null)
                throw new DataException("La categoría indicada no existe.");
        }

        public void ValidarStockMinMax(int? stockMinimo, int? stockMaximo) {
            if (stockMinimo.HasValue && stockMaximo.HasValue && stockMinimo.Value > stockMaximo.Value)
                throw new DataException("El stock mínimo no puede ser mayor al stock máximo.");
        }

        public void ValidarNombre(string nombre) {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new DataException("El nombre es requerido.");
            if (nombre.Trim().Length < 2 || nombre.Trim().Length > 100)
                throw new DataException("El nombre debe tener entre 2 y 100 caracteres.");
        }

        public void ValidarDescripcion(string descripcion) {
            if (descripcion.Length > 500)
                throw new DataException("La descripción no puede tener más de 500 caracteres.");
        }

        public void ValidarCantidad(int cantidad) {
            if (cantidad < 0) throw new DataException("La cantidad no puede ser negativa.");
        }

        public void ValidarPrecio(decimal precio) {
            if (precio < 0) throw new DataException("El precio no puede ser negativo.");
            if (precio > PrecioMaximo)
                throw new DataException("El precio supera el máximo permitido (99.999.999,99).");
        }

        public void ValidarStock(int stock) {
            if (stock < 0) throw new DataException("El stock no puede ser negativo.");
        }

        public void ValidarProveedor(string proveedor) {
            if (proveedor.Length > 150)
                throw new DataException("El proveedor no puede tener más de 150 caracteres.");
        }
    }
}