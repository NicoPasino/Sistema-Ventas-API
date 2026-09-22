using Mapster;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Modelos.Ventas;

namespace NicoPasino.Servicios.Servicios.Ventas
{
    public class CategoriaServicio : IServicioGenerico<Categoria, CategoriaDto, CategoriaDto>
    {
        private readonly IRepositorioGenericoVentas<Categoria> _repoG;
        public CategoriaServicio(IRepositorioGenericoVentas<Categoria> repoG) {
            _repoG = repoG ?? throw new ArgumentNullException(nameof(repoG));
        }

        public async Task<IEnumerable<CategoriaDto>> GetAll(bool activo) {
            var objsDb = await _repoG.ListarAsync(
                orden: q => q.OrderBy(m => m.Nombre)
            );

            // si hay...
            if (objsDb != null && objsDb.Any()) {
                var objsDto = objsDb.Adapt<IEnumerable<CategoriaDto>>();
                return objsDto;
            }
            return Enumerable.Empty<CategoriaDto>();
        }

        public async Task<IEnumerable<CategoriaDto>> GetAll(string campo, string? valor) {
            throw new NotImplementedException();
        }

        public async Task<CategoriaDto> GetById(int id) {
            if (id < 1) throw new DataException("Id no válido.");
            var objDb = await _repoG.GetAsync(filtro: m => m.Id == id);

            if (objDb != null) {
                var objDto = objDb.Adapt<CategoriaDto>();
                return objDto;
            }
            else return new CategoriaDto();
        }

        public async Task<bool> Create(CategoriaDto obj) {
            throw new NotImplementedException();
            /*var objeto = obj.Adapt<Categoria>();
            var res = await _repoG.Add(objeto);

            return (res != null);*/
        }

        public async Task<bool> Update(CategoriaDto obj) {
            throw new NotImplementedException();
            // obtener obj original
            /* if (objDb == null) throw new DataException("Objeto original no encontrado.");

            var res = await _repoG.Update(objeto);
            if (res > 0) return true;
            else throw new UpdateException("No se pudo actualizar en la base de datos.");*/
        }

        public Task<bool> Enable(int id, bool estado) {
            throw new NotImplementedException();
        }
    }
}
