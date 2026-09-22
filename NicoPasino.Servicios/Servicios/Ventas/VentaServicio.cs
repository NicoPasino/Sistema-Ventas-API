using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
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

        public VentaServicio(
            IRepositorioGenericoVentas<Venta> repoG,
            IRepositorioGenericoVentas<Cliente> repoCliente,
            IRepositorioGenericoVentas<Producto> repoProducto,
            IRepositorioGenericoVentas<Ventaporproducto> repoVpp) {
            _repoG = repoG ?? throw new ArgumentNullException(nameof(repoG));
            _repoCliente = repoCliente ?? throw new ArgumentNullException(nameof(repoCliente));
            _repoProducto = repoProducto ?? throw new ArgumentNullException(nameof(repoProducto));
            _repoVpp = repoVpp ?? throw new ArgumentNullException(nameof(repoVpp));
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
            if (string.IsNullOrWhiteSpace(campo)) throw new ArgumentException("Campo de búsqueda no válido.");
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
            if (id <= 0) throw new DataException("Numero de venta no válido"); // TODO: comprobar si existe Nro
            var objDb = await _repoG.GetAsync(
                filtro: m => m.Id == id
                , incluir: "IdClienteNavigation.Venta,Ventaporproducto,Ventaporproducto.IdProductoNavigation"
            );

            if (objDb != null) {
                var objDto = objDb.Adapt<VentaDetalleDto>();
                return objDto;
            }
            else return new VentaDetalleDto();
        }

        public async Task<bool> Create(VentaDto obj) {
            Random random = new();
            if (obj == null) throw new DataException("No se recibió ningún dato.");

            // Verificar Cliente
            if (obj.DNI != null) {
                var cliente = await _repoCliente.GetAsync(filtro: c => c.Documento == obj.DNI);
                if (cliente != null) {
                    obj.IdCliente = cliente.Id;
                }
                else {
                    throw new DataException($"El cliente con el DNI '{obj.DNI}' No existe.");
                }
            }
            else throw new DataException("No se recibió el DNI del cliente.");

            // Verificar productos
            List<Producto> productosValidados;
            int[] ids;
            int[] cants;

            if (obj.ItemsId != null && obj.ItemsCant != null) {
                if (obj.ItemsId.ToArray().Length != obj.ItemsCant.ToArray().Length)
                    throw new DataException("La lista de productos no coincide con la lista de cantidades.");

                ids = obj.ItemsId.ToArray();
                cants = obj.ItemsCant.ToArray();
                productosValidados = new List<Producto>(ids.Length);

                for (int i = 0; i < ids.Length; i++) {
                    var idPublica = ids[i];
                    var cantidad = cants[i];

                    var productoDb = await _repoProducto.GetAsync(filtro: p => p.IdPublica == idPublica);
                    if (productoDb == null)
                        throw new DataException($"Producto no encontrado (código del producto: {idPublica}).");

                    if (cantidad > productoDb.Cantidad)
                        throw new DataException(
                            $"Stock insuficiente para '{productoDb.Nombre}', cantidad solicitada: '{cantidad}', disponible: '{productoDb.Cantidad}'.");

                    productosValidados.Add(productoDb);
                }
            }
            else throw new DataException("No se recibió la lista de productos.");

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

        public Task<bool> Update(VentaDto obj) {
            throw new NotImplementedException();
        }

        public Task<bool> Enable(int id, bool estado) {
            throw new NotImplementedException();
        }
    }
}