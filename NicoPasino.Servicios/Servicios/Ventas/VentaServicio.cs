using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using System.Linq.Expressions;

namespace NicoPasino.Servicios.Servicios.Ventas
{
    public class VentaServicio : IServicioGenerico<Venta, VentaDto, VentaDetalleDto>
    {
        // TODO: usar uow?
        private readonly IRepositorioGenericoVentas<Venta> _repoG;
        private readonly IRepositorioGenericoVentas<Cliente> _repoCliente;
        private readonly IRepositorioGenericoVentas<Producto> _repoProducto;
        private readonly IRepositorioGenericoVentas<Ventaporproducto> _repoVpp;
        private readonly VentaValidador _validador;

        public VentaServicio(
            IRepositorioGenericoVentas<Venta> repoG,
            IRepositorioGenericoVentas<Cliente> repoCliente,
            IRepositorioGenericoVentas<Producto> repoProducto,
            IRepositorioGenericoVentas<Ventaporproducto> repoVpp,
            VentaValidador validador) {
            _repoG = repoG ?? throw new ArgumentNullException(nameof(repoG));
            _repoCliente = repoCliente ?? throw new ArgumentNullException(nameof(repoCliente));
            _repoProducto = repoProducto ?? throw new ArgumentNullException(nameof(repoProducto));
            _repoVpp = repoVpp ?? throw new ArgumentNullException(nameof(repoVpp));
            _validador = validador ?? throw new ArgumentNullException(nameof(validador));
        }

        public async Task<IEnumerable<VentaDetalleDto>> GetAll(bool activo) {
            var objsDb = await _repoG.ListarAsync(
                //filtro: m => m.Activo == activo
                orden: q => q.OrderByDescending(m => m.FechaVenta)
                , incluir: "IdClienteNavigation.Venta,Ventaporproducto,Ventaporproducto.IdProductoNavigation"
            );

            // si hay...
            if (objsDb != null && objsDb.Any()) {
                var objsDto = objsDb.Adapt<IEnumerable<VentaDetalleDto>>();
                return objsDto;
            }
            else return Enumerable.Empty<VentaDetalleDto>();
        }

        public async Task<IEnumerable<VentaDetalleDto>> GetAll(string campo, string? valor) {
            if (string.IsNullOrWhiteSpace(campo)) throw new DataException("Campo de búsqueda no válido.");
            campo = campo.Trim().ToLowerInvariant();
            valor = valor?.Trim();

            Expression<Func<Venta, bool>> filtro = null;
            Func<IQueryable<Venta>, IOrderedQueryable<Venta>> orden = null;

            bool valorIsNull = string.IsNullOrEmpty(valor);
            var vLower = valorIsNull ? string.Empty : valor!.ToLowerInvariant();

            switch (campo) {
                case "numero":
                    orden = q => q.OrderBy(m => m.Numero);
                    if (!valorIsNull) {
                        filtro = m => m.Numero != null
                                    && m.Numero.ToString().Contains(vLower);
                    }
                    break;

                case "nombre":
                case "nombredesc":
                    orden = (campo == "nombre")
                        ? new Func<IQueryable<Venta>, IOrderedQueryable<Venta>>(q => q.OrderBy(m => m.IdClienteNavigation.Nombre))
                        : new Func<IQueryable<Venta>, IOrderedQueryable<Venta>>(q => q.OrderByDescending(m => m.IdClienteNavigation.Nombre));
                    if (!valorIsNull) {
                        filtro = m => m.IdClienteNavigation != null
                                   && m.IdClienteNavigation.Nombre != null
                                   && m.IdClienteNavigation.Nombre.ToLower().Contains(vLower);
                    }
                    break;

                case "otro":
                case "otrodesc":
                    orden = (campo == "otro")
                        ? new Func<IQueryable<Venta>, IOrderedQueryable<Venta>>(q => q.OrderBy(m => m.Detalle))
                        : new Func<IQueryable<Venta>, IOrderedQueryable<Venta>>(q => q.OrderByDescending(m => m.Detalle));
                    if (!valorIsNull) {
                        filtro = m => m.Detalle != null
                                    && m.Detalle.ToLower().Contains(vLower);
                    }
                    break;

                default:
                    throw new DataException($"Campo de búsqueda '{campo}' no soportado. Campos soportados: numero, nombre(Cliente), otro(Detalle).");
            }

            var objsDb = await _repoG.ListarAsync(filtro: filtro, incluir: "IdClienteNavigation.Venta,Ventaporproducto,Ventaporproducto.IdProductoNavigation", orden: orden);

            if (objsDb != null && objsDb.Any()) {
                var objsDto = objsDb.Adapt<IEnumerable<VentaDetalleDto>>();
                return objsDto;
            }
            else return Enumerable.Empty<VentaDetalleDto>();
        }

        public async Task<VentaDetalleDto> GetById(int id) {
            _validador.ValidarIdOperacion(id);

            var objDb = await _repoG.GetAsync(
                filtro: m => m.Id == id
                , incluir: "IdClienteNavigation.Venta,Ventaporproducto,Ventaporproducto.IdProductoNavigation"
            );

            if (objDb != null) {
                var objDto = objDb.Adapt<VentaDetalleDto>();
                return objDto;
            }
            else return null!;
        }

        public async Task<bool> Create(VentaDto obj) {
            Random random = new();
            _validador.NormalizarYValidar(obj);

            // Verificar Cliente (los ítems referencian IdCliente, no el DNI)
            obj.IdCliente = await _validador.ValidarClienteAsync(obj.DNI);

            // Verificar productos (existencia, estado y stock)
            var ids = obj.ItemsId!.ToArray();
            var cants = obj.ItemsCant!.ToArray();
            var productosValidados = await _validador.ValidarProductosAsync(ids, cants);

            // Mapear a Venta manualmente para evitar conflictos de tipos
            var venta = new Venta
            {
                IdCliente = obj.IdCliente,
                Numero = random.Next(1, 9999999),
                Detalle = obj.Detalle,
                FechaVenta = obj.FechaVenta ?? DateTime.UtcNow
            };

            // Guardar venta
            var ventaGuardada = await _repoG.Add(venta);
            if (ventaGuardada == null) throw new UpdateException("Error al guardar la venta.");

            // Guardar VentaPorProducto usando productos cacheados
            for (int i = 0; i < ids.Length; i++) {
                var producto = productosValidados[i];
                var cantidad = cants[i];

                var vpp = new Ventaporproducto
                {
                    IdVenta = ventaGuardada.Id,
                    IdProducto = producto.Id,
                    NombreProducto = producto.Nombre,
                    Cantidad = cantidad,
                    PrecioUnitario = producto.Precio,
                    SubTotal = producto.Precio * cantidad
                };

                await _repoVpp.Add(vpp);
            }

            // descontar stock
            for (int i = 0; i < productosValidados.Count; i++) {
                var producto = productosValidados[i];
                producto.Cantidad -= cants[i];

                var res = await _repoProducto.Update(producto);
                if (res <= 0) throw new UpdateException("No se pudo actualizar el stock del producto.");
            }
            return true;
        }

        public async Task<bool> Update(VentaDto obj) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");
            if (!obj.Id.HasValue || obj.Id.Value <= 0) throw new DataException("Numero de venta no válido");

            // los ítems y el cliente no son editables: obligar a rehacer la venta
            if (obj.ItemsId != null || obj.ItemsCant != null)
                throw new DataException("No se pueden modificar los productos de una venta existente.");

            if (obj.Detalle != null) {
                obj.Detalle = obj.Detalle.Trim();
                _validador.ValidarDetalle(obj.Detalle);
                obj.Detalle = string.IsNullOrEmpty(obj.Detalle) ? null : obj.Detalle;
            }
            _validador.ValidarFecha(obj.FechaVenta);

            var objDb = await _repoG.GetAsync(filtro: m => m.Id == obj.Id.Value);
            if (objDb == null) throw new DataException("Venta no encontrada.");

            if (obj.Detalle == objDb.Detalle
                && (obj.FechaVenta == null || obj.FechaVenta == objDb.FechaVenta)) {
                return true; // sin cambios, no tocar la BD
            }

            if (obj.Detalle != null) objDb.Detalle = obj.Detalle;
            if (obj.FechaVenta.HasValue) objDb.FechaVenta = obj.FechaVenta;

            var res = await _repoG.Update(objDb);
            if (res > 0) return true;
            else throw new UpdateException("No se pudo actualizar en la base de datos.");
        }

        public Task<bool> Enable(int id, bool estado) {
            // La tabla 'venta' no tiene columna 'activo' (baja lógica no modelada).
            // Habilitarlo requiere agregar la propiedad y una migración.
            throw new NotImplementedException("La baja de ventas no está implementada: la tabla 'venta' no tiene columna 'activo'.");
        }
    }
}