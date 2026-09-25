using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace NicoPasino.Servicios.Servicios.Ventas
{
    public class ClienteServicio : IServicioGenerico<Cliente, ClienteDto, ClienteDto>
    {
        private readonly IRepositorioGenericoVentas<Cliente> _repoG;
        public ClienteServicio(IRepositorioGenericoVentas<Cliente> repoG) {
            _repoG = repoG ?? throw new ArgumentNullException(nameof(repoG));
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
            if (string.IsNullOrWhiteSpace(campo)) throw new ArgumentException("Campo de búsqueda no válido.");
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
            if (dni <= 9999999 || dni > 999999999) throw new DataException("Documento no válido.");
            var objDb = await _repoG.GetAsync(filtro: m => m.Documento == dni, incluir: "Venta");

            if (objDb != null) {
                var objDto = objDb.Adapt<ClienteDto>();
                return objDto;
            }
            else return new ClienteDto();
        }

        public async Task<bool> Create(ClienteDto obj) {
            ValidarDatos(obj);

            var existente = await _repoG.GetAsync(filtro: c => c.Documento == obj.Documento || c.Correo == obj.Correo);
            if (existente != null) {
                if (existente.Documento == obj.Documento)
                    throw new DataException($"Ya existe un cliente con el DNI '{obj.Documento}'.");
                if (existente.Correo == obj.Correo)
                    throw new DataException($"Ya existe un cliente con el correo '{obj.Correo}'.");
            }

            var objeto = obj.Adapt<Cliente>();
            objeto.Activo = true;
            objeto.Telefono = obj.Telefono;
            var res = await _repoG.Add(objeto);

            return (res != null);
        }

        public async Task<bool> Update(ClienteDto obj) {
            ValidarDatos(obj);

            // obtener obj original
            var objDb = await _repoG.GetAsync(filtro: x => x.Documento == obj.Documento, incluir: "Venta");
            if (objDb == null) throw new DataException("Objeto original no encontrado.");

            // verificar duplicados excluyendo el registro actual
            var duplicados = await _repoG.ListarAsync(filtro: c =>
                (c.Documento == obj.Documento || c.Correo == obj.Correo) && c.Id != objDb.Id);
            var dup = duplicados.FirstOrDefault();
            if (dup != null) {
                if (dup.Documento == obj.Documento)
                    throw new DataException($"Ya existe otro cliente con el DNI '{obj.Documento}'.");
                if (dup.Correo == obj.Correo)
                    throw new DataException($"Ya existe otro cliente con el correo '{obj.Correo}'.");
            }

            // mapear
            var objeto = obj.Adapt<Cliente>();

            // subir
            var res = await _repoG.Update(objeto);
            if (res > 0) return true;
            else throw new UpdateException("No se pudo actualizar en la base de datos.");
        }


        private void ValidarDatos(ClienteDto obj) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");
            if (obj.Documento < 10000000 || obj.Documento > 99999999) throw new DataException("Documento no válido.");
            if (string.IsNullOrWhiteSpace(obj.Nombre) || obj.Nombre.Trim().Length < 4) throw new DataException("Nombre no válido.");
            if (string.IsNullOrWhiteSpace(obj.Correo) || obj.Correo.Trim().Length < 5)
                throw new DataException("Correo no válido.");
            if (!new EmailAddressAttribute().IsValid(obj.Correo.Trim()))
                throw new DataException("Correo no válido.");
        }




        public async Task<bool> Patch(ClientePatchDto obj, int documento) {
            if (obj == null) throw new DataException("No se recibió ningún dato.");
            if (documento < 10000000 || documento > 99999999) throw new DataException("Documento no válido.");

            if (obj.Nombre == null
                && obj.Correo == null
                && obj.Telefono == null
                && obj.Activo == null) throw new DataException("No se recibieron datos para actualizar.");

            var objDb = await _repoG.GetAsync(filtro: c => c.Documento == documento);
            if (objDb == null) throw new DataException("Cliente no encontrado.");

            if (obj.Nombre != null) {
                if (string.IsNullOrWhiteSpace(obj.Nombre) || obj.Nombre.Trim().Length < 4) throw new DataException("Nombre no válido.");
                objDb.Nombre = obj.Nombre;
            }
            if (obj.Correo != null) {
                obj.Correo = obj.Correo.Trim();
                if (obj.Correo.Length < 5 || !new EmailAddressAttribute().IsValid(obj.Correo))
                    throw new DataException("Correo no válido.");

                var duplicados = await _repoG.ListarAsync(filtro: c =>
                    c.Correo == obj.Correo && c.Id != objDb.Id);
                if (duplicados.Any())
                    throw new DataException($"Ya existe otro cliente con el correo '{obj.Correo}'.");

                objDb.Correo = obj.Correo;
            }
            if (obj.Telefono != null) objDb.Telefono = obj.Telefono;
            if (obj.Activo.HasValue) objDb.Activo = obj.Activo.Value;

            var res = await _repoG.Update(objDb);
            if (res > 0) return true;
            else throw new UpdateException("No se pudo actualizar en la base de datos.");
        }

        public async Task<bool> Enable(int id, bool estado) {
            if (id <= 0) throw new DataException("Documento no válido");
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