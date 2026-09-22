namespace NicoPasino.Core.Interfaces
{
    public interface IServicioGenerico<TEntity, TEntradaDto, TSalidaDto> where TEntity : class where TEntradaDto : class where TSalidaDto : class
    {
        Task<IEnumerable<TSalidaDto>> GetAll(bool activo);
        Task<IEnumerable<TSalidaDto>> GetAll(string campo, string? valor); // búsqueda por campo
        Task<TSalidaDto> GetById(int id);
        Task<bool> Create(TEntradaDto obj);
        Task<bool> Update(TEntradaDto obj);
        Task<bool> Enable(int id, bool estado);
    }
}
