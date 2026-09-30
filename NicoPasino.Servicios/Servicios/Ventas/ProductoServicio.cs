using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using System.Linq.Expressions;

namespace NicoPasino.Servicios.Servicios.Ventas
{
    public class ProductoServicio : IServicioGenerico<Producto, ProductoDto, ProductoDto>
    {
        private const int IntentosIdPublica = 10;

        private readonly IRepositorioGenericoVentas<Producto> _repoG;
        private readonly ProductoValidador _validador;

        public ProductoServicio(IRepositorioGenericoVentas<Producto> repoG,
                                 ProductoValidador validador) {
            _repoG = repoG ?? throw new ArgumentNullException(nameof(repoG));
            _validador = validador ?? throw new ArgumentNullException(nameof(validador));
        }

        public async Task<IEnumerable<ProductoDto>> GetAll(bool activo) {
            try {
                var objsDb = await _repoG.ListarAsync(
                    //filtro: m => m.Activo == activo // Se filtra desde Front.
                    orden: q => q.OrderByDescending(m => m.FechaModificacion)
                    , incluir: "IdCategoriaNavigation"
                );

                // si hay...
                if (objsDb != null && objsDb.Any()) {
                    var objsDto = objsDb.Adapt<IEnumerable<ProductoDto>>();
                    return objsDto;
                }
                return Enumerable.Empty<ProductoDto>();
            }
            catch (Exception ex) {
                throw new Exception(ex.Message);
            }
        }

        public async Task<IEnumerable<ProductoDto>> GetAll(string campo, string? valor) {
            if (string.IsNullOrWhiteSpace(campo)) throw new ArgumentException("Campo de búsqueda no válido.");
            campo = campo.Trim().ToLowerInvariant();
            valor = valor?.Trim();

            Expression<Func<Producto, bool>> filtro = null;
            Func<IQueryable<Producto>, IOrderedQueryable<Producto>> orden = null;

            bool valorIsNull = string.IsNullOrEmpty(valor);
            var vLower = valorIsNull ? string.Empty : valor!.ToLowerInvariant();

            switch (campo) {
                case "numero":
                    orden = q => q.OrderBy(m => m.Id);
                    if (!valorIsNull) {
                        filtro = m => m.Activo
                                    && m.IdPublica != null
                                    && m.IdPublica.ToString().Contains(vLower);
                    }
                    break;

                case "nombre":
                case "nombredesc":
                    orden = (campo == "nombre")
                        ? new Func<IQueryable<Producto>, IOrderedQueryable<Producto>>(q => q.OrderBy(m => m.Nombre))
                        : new Func<IQueryable<Producto>, IOrderedQueryable<Producto>>(q => q.OrderByDescending(m => m.Nombre));
                    if (!valorIsNull) {
                        filtro = m => m.Activo
                                    && m.Nombre != null
                                    && m.Nombre.ToLower().Contains(vLower);
                    }
                    break;

                case "otro":
                case "otrodesc":
                    orden = (campo == "otro")
                        ? new Func<IQueryable<Producto>, IOrderedQueryable<Producto>>(q => q.OrderBy(m => m.IdCategoriaNavigation.Nombre))
                        : new Func<IQueryable<Producto>, IOrderedQueryable<Producto>>(q => q.OrderByDescending(m => m.IdCategoriaNavigation.Nombre));
                    if (!valorIsNull) {
                        filtro = m => m.Activo
                                    && m.IdCategoriaNavigation != null
                                    && m.IdCategoriaNavigation.Nombre != null
                                    && m.IdCategoriaNavigation.Nombre.ToLower().Contains(vLower);
                    }
                    break;

                case "proveedor":
                case "proveedordesc":
                    orden = (campo == "proveedor")
                        ? new Func<IQueryable<Producto>, IOrderedQueryable<Producto>>(q => q.OrderBy(m => m.Proveedor))
                        : new Func<IQueryable<Producto>, IOrderedQueryable<Producto>>(q => q.OrderByDescending(m => m.Proveedor));
                    if (!valorIsNull) {
                        filtro = m => m.Activo
                                    && m.Proveedor != null
                                    && m.Proveedor.ToLower().Contains(vLower);
                    }
                    break;

                default:
                    throw new DataException($"Campo de búsqueda '{campo}' no soportado. Campos soportados: numero, nombre, otro(Nombre Categoría), proveedor(Nombre Proveedor).");
            }

            var objsDb = await _repoG.ListarAsync(filtro: filtro, incluir: "IdCategoriaNavigation", orden: orden);

            if (objsDb != null && objsDb.Any()) {
                var objsDto = objsDb.Adapt<IEnumerable<ProductoDto>>();
                return objsDto;
            }
            else return Enumerable.Empty<ProductoDto>();
        }

        public async Task<ProductoDto> GetById(int id) {
            if (id <= 0) throw new DataException("Id no válido");
            var objDb = await _repoG.GetAsync(
                filtro: m => m.IdPublica == id
                , incluir: "IdCategoriaNavigation"
            );

            if (objDb != null) {
                var objDto = objDb.Adapt<ProductoDto>();
                return objDto;
            }
            else return new ProductoDto();
        }

        public async Task<bool> Create(ProductoDto obj) {
            _validador.NormalizarYValidar(obj);
            await _validador.ValidarCategoriaAsync(obj.IdCategoria);

            obj.IdPublica = await GenerarIdPublicaUnicoAsync();
            var objeto = obj.Adapt<Producto>();

            objeto.FechaCreacion = DateTime.UtcNow;
            objeto.FechaModificacion = DateTime.UtcNow;
            objeto.Activo = true;

            // Asegurar que se use sólo la FK y evitar que EF intente insertar una nueva Categoria
            objeto.IdCategoria = obj.IdCategoria;
            objeto.IdCategoriaNavigation = null;

            var res = await _repoG.Add(objeto);

            return (res != null);
        }

        public async Task<bool> Update(ProductoDto obj) {
            _validador.NormalizarYValidar(obj);
            if (obj.IdPublica == null || obj.IdPublica <= 0) throw new DataException("No se recibió un ID válido.");
            await _validador.ValidarCategoriaAsync(obj.IdCategoria);

            // obtener obj original
            var objDb = await _repoG.GetAsync(filtro: x => x.IdPublica == obj.IdPublica/*, incluir: "IdCategoriaNavigation"*/);
            if (objDb == null) throw new DataException("Objeto original no encontrado.");

            // si no hay cambios, informar sin tocar la BD
            if (obj.Nombre == objDb.Nombre
                && obj.Descripcion == objDb.Descripcion
                && obj.IdCategoria == objDb.IdCategoria
                && obj.Cantidad == objDb.Cantidad
                && obj.Precio == objDb.Precio
                && obj.StockMinimo == objDb.StockMinimo
                && obj.StockMaximo == objDb.StockMaximo
                && obj.Proveedor == objDb.Proveedor) {
                return true;
            }

            // Mapear objeto
            var objMapped = obj.Adapt<Producto>();

            // Asegurar que se use sólo la FK y evitar que EF intente insertar una nueva Categoria
            objMapped.IdCategoria = obj.IdCategoria;
            objMapped.IdCategoriaNavigation = null;

            objMapped.Id = objDb.Id;
            objMapped.FechaCreacion = objDb.FechaCreacion;
            objMapped.FechaModificacion = DateTime.UtcNow;

            // Subir
            var res = await _repoG.Update(objMapped);
            if (res > 0) return true;
            else throw new UpdateException("No pudo actualizar en la base de datos.");
        }

        public async Task<bool> Patch(ProductoPatchDto obj, int idPublica) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");
            if (idPublica <= 0) throw new DataException("No se recibió ningún ID.");

            if (obj.Nombre == null
                && obj.Descripcion == null
                && obj.Cantidad == null
                && obj.Precio == null
                && obj.IdCategoria == null
                && obj.Activo == null
                && obj.StockMinimo == null
                && obj.StockMaximo == null
                && obj.Proveedor == null) throw new DataException("No se recibieron datos para actualizar.");

            var objDb = await _repoG.GetAsync(filtro: x => x.IdPublica == idPublica);
            if (objDb == null) throw new DataException("Producto no encontrado.");

            if (obj.Nombre != null) {
                _validador.ValidarNombre(obj.Nombre);
                objDb.Nombre = obj.Nombre.Trim();
            }
            if (obj.Descripcion != null) {
                obj.Descripcion = obj.Descripcion.Trim();
                _validador.ValidarDescripcion(obj.Descripcion);
                objDb.Descripcion = string.IsNullOrEmpty(obj.Descripcion) ? null : obj.Descripcion;
            }
            if (obj.Cantidad.HasValue) {
                _validador.ValidarCantidad(obj.Cantidad.Value);
                objDb.Cantidad = obj.Cantidad.Value;
            }
            if (obj.Precio.HasValue) {
                _validador.ValidarPrecio(obj.Precio.Value);
                objDb.Precio = obj.Precio.Value;
            }
            if (obj.StockMinimo.HasValue) {
                _validador.ValidarStock(obj.StockMinimo.Value);
                objDb.StockMinimo = obj.StockMinimo.Value;
            }
            if (obj.StockMaximo.HasValue) {
                _validador.ValidarStock(obj.StockMaximo.Value);
                objDb.StockMaximo = obj.StockMaximo.Value;
            }

            // el chequeo cruzado usa el valor efectivo (payload ?: valor ya persistido)
            _validador.ValidarStockMinMax(obj.StockMinimo ?? objDb.StockMinimo, obj.StockMaximo ?? objDb.StockMaximo);

            if (obj.Proveedor != null) {
                obj.Proveedor = obj.Proveedor.Trim();
                _validador.ValidarProveedor(obj.Proveedor);
                objDb.Proveedor = string.IsNullOrEmpty(obj.Proveedor) ? null : obj.Proveedor;
            }
            if (obj.IdCategoria.HasValue) {
                if (obj.IdCategoria.Value < 1) throw new DataException("La categoría no es válida.");
                await _validador.ValidarCategoriaAsync(obj.IdCategoria.Value);
                objDb.IdCategoria = obj.IdCategoria.Value;
                objDb.IdCategoriaNavigation = null;
            }
            if (obj.Activo.HasValue) objDb.Activo = obj.Activo.Value;

            objDb.FechaModificacion = DateTime.UtcNow;

            var res = await _repoG.Update(objDb);
            if (res > 0) return true;
            else throw new UpdateException("No pudo actualizar en la base de datos.");
        }

        private async Task<int?> GenerarIdPublicaUnicoAsync() {
            for (int i = 0; i < IntentosIdPublica; i++) {
                var idPublica = Random.Shared.Next(1, 9999999);
                var existente = await _repoG.GetAsync(filtro: p => p.IdPublica == idPublica);
                if (existente == null) return idPublica;
            }
            throw new DataException("No se pudo generar un código único para el producto, reintentar.");
        }

        public async Task<bool> Enable(int id, bool estado) {
            if (id <= 0) throw new DataException("Id no válido");
            var objDb = await _repoG.GetAsync(filtro: m => m.IdPublica == id);

            if (objDb != null) {
                objDb.FechaModificacion = DateTime.UtcNow;
                objDb.Activo = !objDb.Activo;

                await _repoG.Update(objDb);
                //await _uow.SaveChangesAsync();
                return true;
            }
            else throw new DataException("Id no válido");
        }
    }
}