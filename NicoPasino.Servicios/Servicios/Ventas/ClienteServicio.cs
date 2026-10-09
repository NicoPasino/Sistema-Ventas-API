using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using System.Linq.Expressions;

namespace NicoPasino.Servicios.Servicios.Ventas
{
    public class ClienteServicio : IServicioGenerico<Cliente, ClienteDto, ClienteDto>
    {
        private readonly IRepositorioGenericoVentas<Cliente> _repoG;
        private readonly ClienteValidador _validador;

        public ClienteServicio(IRepositorioGenericoVentas<Cliente> repoG,
                               ClienteValidador validador) {
            _repoG = repoG ?? throw new ArgumentNullException(nameof(repoG));
            _validador = validador ?? throw new ArgumentNullException(nameof(validador));
        }

        public async Task<IEnumerable<ClienteDto>> GetAll(bool activo) {
            var objsDb = await _repoG.ListarAsync(
            //filtro: m => m.Activo == activo
            orden: q => q.OrderByDescending(m => m.FechaCreacion),
            incluir: "Venta"
            );

            // si hay...
            if (objsDb != null && objsDb.Any()) {
                var objsDto = objsDb.Adapt<IEnumerable<ClienteDto>>();
                return objsDto;
            }
            return Enumerable.Empty<ClienteDto>();
        }

        public async Task<IEnumerable<ClienteDto>> GetAll(string campo, string? valor) {
            if (string.IsNullOrWhiteSpace(campo)) throw new DataException("Campo de búsqueda no válido.");
            campo = campo.Trim().ToLowerInvariant();
            valor = valor?.Trim();

            Expression<Func<Cliente, bool>> filtro = null;
            Func<IQueryable<Cliente>, IOrderedQueryable<Cliente>> orden = null;

            bool valorIsNull = string.IsNullOrEmpty(valor);
            var vLower = valorIsNull ? string.Empty : valor!.ToLowerInvariant();

            switch (campo) {
                case "numero":
                case "numerodesc":
                    orden = (campo == "numero")
                        ? new Func<IQueryable<Cliente>, IOrderedQueryable<Cliente>>(q => q.OrderBy(m => m.Documento))
                        : new Func<IQueryable<Cliente>, IOrderedQueryable<Cliente>>(q => q.OrderByDescending(m => m.Documento));

                    if (!valorIsNull) {
                        filtro = m => m.Documento != null
                                    && m.Documento.ToString().Contains(vLower);
                    }
                    break;

                case "nombre":
                case "nombredesc":
                    orden = (campo == "nombre")
                        ? new Func<IQueryable<Cliente>, IOrderedQueryable<Cliente>>(q => q.OrderBy(m => m.Nombre))
                        : new Func<IQueryable<Cliente>, IOrderedQueryable<Cliente>>(q => q.OrderByDescending(m => m.Nombre));

                    if (!valorIsNull) {
                        filtro = m => m.Nombre != null
                                    && m.Nombre.ToLower().Contains(vLower);
                    }
                    break;

                case "otro":
                case "otrodesc":
                    orden = (campo == "otro")
                        ? new Func<IQueryable<Cliente>, IOrderedQueryable<Cliente>>(q => q.OrderBy(m => m.Correo))
                        : new Func<IQueryable<Cliente>, IOrderedQueryable<Cliente>>(q => q.OrderByDescending(m => m.Correo));

                    if (!valorIsNull) {
                        filtro = m => m.Correo != null
                                   && m.Correo.ToLower().Contains(vLower);
                    }
                    break;

                default:
                    throw new DataException($"Campo de búsqueda '{campo}' no soportado. Campos soportados: numero(Documento), nombre, otro(Correo).");
            }

            var objsDb = await _repoG.ListarAsync(filtro: filtro, incluir: "Venta", orden: orden);

            if (objsDb != null && objsDb.Any()) {
                var objsDto = objsDb.Adapt<IEnumerable<ClienteDto>>();
                return objsDto;
            }
            else return Enumerable.Empty<ClienteDto>();
        }

        public async Task<ClienteDto> GetById(int dni) {
            _validador.ValidarDocumento(dni);

            var objDb = await _repoG.GetAsync(filtro: m => m.Documento == dni, incluir: "Venta");

            if (objDb != null) {
                var objDto = objDb.Adapt<ClienteDto>();
                return objDto;
            }
            else return null!;
        }

        public async Task<bool> Create(ClienteDto obj) {
            _validador.NormalizarYValidar(obj);
            await _validador.ValidarDuplicadosAsync(obj);

            var objeto = obj.Adapt<Cliente>();
            objeto.Activo = true;
            objeto.FechaCreacion = DateTime.UtcNow;
            objeto.Telefono = obj.Telefono;
            var res = await _repoG.Add(objeto);

            return (res != null);
        }

        public async Task<bool> Update(ClienteDto obj) {
            _validador.NormalizarYValidar(obj);

            // obtener obj original
            var objDb = await _repoG.GetAsync(filtro: x => x.Documento == obj.Documento, incluir: "Venta");
            if (objDb == null) throw new DataException("Objeto original no encontrado.");

            await _validador.ValidarDuplicadosAsync(obj, objDb.Id);

            // mapear
            var objeto = obj.Adapt<Cliente>();

            // conservar datos que no llegan en el dto
            objeto.Id = objDb.Id;
            objeto.FechaCreacion = objDb.FechaCreacion;

            // subir
            var res = await _repoG.Update(objeto);
            if (res > 0) return true;
            else throw new UpdateException("No se pudo actualizar en la base de datos.");
        }

        public async Task<bool> Patch(ClientePatchDto obj, int documento) {
            _validador.NormalizarYValidarPatch(obj);
            _validador.ValidarDocumento(documento);

            var objDb = await _repoG.GetAsync(filtro: c => c.Documento == documento);
            if (objDb == null) throw new DataException("Cliente no encontrado.");

            if (obj.Nombre != null) objDb.Nombre = obj.Nombre;
            if (obj.Correo != null) {
                await _validador.ValidarCorreoUnicoAsync(obj.Correo, objDb.Id);
                objDb.Correo = obj.Correo;
            }
            if (obj.Telefono != null) objDb.Telefono = obj.Telefono;
            if (obj.Activo.HasValue) objDb.Activo = obj.Activo.Value;

            var res = await _repoG.Update(objDb);
            if (res > 0) return true;
            else throw new UpdateException("No se pudo actualizar en la base de datos.");
        }

        public async Task<bool> Enable(int id, bool estado) {
            _validador.ValidarDocumento(id);

            var objDb = await _repoG.GetAsync(filtro: m => m.Documento == id);

            if (objDb != null) {
                objDb.Activo = estado;
                await _repoG.Update(objDb);
                return true;
            }
            else throw new DataException("Documento no válido");
        }

        /*public async Task<bool> Enable(int id, bool estado) {
            if (id <= 0) throw new ArgumentException("Id no válido");
            var objDb = await _repoG.GetAsync(filtro: m => m.Id == id);

            if (objDb != null) {
                //objDb.FechaModificacion = DateTime.UtcNow;
                objDb.Activo = estado;

                await _repoG.Update(objDb);
                //await _uow.SaveChangesAsync();
                return true;
            }
            else throw new ArgumentException("Id no válido");
        }*/
    }
}