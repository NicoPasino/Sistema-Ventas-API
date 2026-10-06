using System.Linq.Expressions;
using System.Reflection;
using NicoPasino.Core.Interfaces;

namespace NicoPasino.Tests.Common.Fakes;

/// <summary>
/// Implementacion en memoria de <see cref="IRepositorioGenericoVentas{T}"/>.
/// <para>
/// A diferencia de un mock, este fake <b>evalua de verdad</b> el
/// <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> de filtro y el delegate de orden.
/// Eso es lo que permite verificar que los servicios piden el orden correcto
/// (<c>OrderByDescending(m =&gt; m.FechaModificacion)</c>) y filtran por los
/// campos correctos.
/// </para>
/// <para>
/// <b>Semantica de tracking.</b> Se replica la diferencia que hay en la
/// implementacion real (<c>RepositorioGenericoVentas.cs</c>):
/// <list type="bullet">
///   <item><description><c>GetAsync</c> devuelve una <b>copia</b> (equivale al
///   <c>AsNoTracking()</c> que aplica la linea 59). Mutar el resultado NO cambia
///   el estado almacenado: hay que llamar a <c>Update</c>.</description></item>
///   <item><description><c>ListarAsync</c>, <c>GetAll</c> y <c>GetById</c> devuelven
///   las instancias reales (trackeadas, como las lineas 72, 19 y 22).</description></item>
/// </list>
/// Asi, un servicio que muta la entidad pero olvida llamar a <c>Update</c>
/// queda detectado.
/// </para>
/// <para>
/// <b>Limitacion conocida.</b> El parametro <c>incluir</c> se <b>registra pero no
/// navega</c>: no hay base de datos que resuelva las relaciones. Las navegaciones
/// tienen que venir cableadas en la entidad sembrada (ver los builders).
/// </para>
/// </summary>
public sealed class RepositorioGenericoVentasFake<T> : IRepositorioGenericoVentas<T> where T : class
{
    private readonly List<T> _entidades = new();
    private readonly PropertyInfo? _propiedadId = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
    private int _siguienteId = 1;

    public RepositorioGenericoVentasFake(params T[] entidades) => _entidades.AddRange(entidades);

    // ---------------------------------------------------------------- estado

    /// <summary>Entidades almacenadas. Solo para lectura desde los tests.</summary>
    public IReadOnlyList<T> Entidades => _entidades;

    public int VecesGetAsync { get; private set; }
    public int VecesGetAll { get; private set; }
    public int VecesGetById { get; private set; }
    public int VecesListar { get; private set; }
    public int VecesAdd { get; private set; }
    public int VecesUpdate { get; private set; }
    public int VecesDelete { get; private set; }

    public T? UltimaEntidadAgregada { get; private set; }
    public T? UltimaEntidadActualizada { get; private set; }
    public T? UltimaEntidadEliminada { get; private set; }

    /// <summary>Ultimo valor de <c>incluir</c> recibido (string CSV de rutas de Include).</summary>
    public string? UltimoInclude { get; private set; }

    /// <summary>Ultimo filtro recibido, para inspeccionarlo en el test.</summary>
    public Expression<Func<T, bool>>? UltimoFiltro { get; private set; }

    /// <summary>Ultimo delegate de orden recibido.</summary>
    public Func<IQueryable<T>, IOrderedQueryable<T>>? UltimoOrden { get; private set; }

    // ---------------------------------------------------------------- ganchos

    /// <summary>
    /// Si se asigna, <see cref="GetAsync"/> devuelve lo que retorne este delegate
    /// en vez de consultar el almacen. Permite simular secuencias (por ejemplo,
    /// los 10 reintentos de <c>GenerarIdPublicaUnicoAsync</c>).
    /// </summary>
    public Func<Expression<Func<T, bool>>?, T?>? AlGetAsync { get; set; }

    /// <summary>
    /// Igual que <see cref="AlGetAsync"/> pero para <see cref="ListarAsync"/>. Permite
    /// simular que la consulta falla (por ejemplo, para verificar como el servicio
    /// envuelve las excepciones).
    /// </summary>
    public Func<IEnumerable<T>>? AlListarAsync { get; set; }

    /// <summary>Si se asigna, <see cref="Add"/> devuelve lo que retorne este delegate.</summary>
    public Func<T, T?>? AlAdd { get; set; }

    /// <summary>Valor por defecto que devuelve <see cref="Update"/> (filas afectadas).</summary>
    public int ResultadoUpdate { get; set; } = 1;

    /// <summary>
    /// Si tiene elementos, <see cref="Update"/> va consumiendo de la cola en vez de
    /// usar <see cref="ResultadoUpdate"/>. Sirve para simular un fallo parcial
    /// (por ejemplo, que el segundo producto de una venta no se pueda descontar).
    /// </summary>
    public Queue<int> ResultadosUpdate { get; } = new();

    // ---------------------------------------------------------------- helpers

    public void Semear(params T[] entidades) => _entidades.AddRange(entidades);

    public void Limpiar()
    {
        _entidades.Clear();
        VecesGetAsync = VecesGetAll = VecesGetById = VecesListar = VecesAdd = VecesUpdate = VecesDelete = 0;
        UltimaEntidadAgregada = UltimaEntidadActualizada = UltimaEntidadEliminada = null;
        UltimoInclude = null;
        UltimoFiltro = null;
        UltimoOrden = null;
        AlGetAsync = null;
        AlListarAsync = null;
        AlAdd = null;
        ResultadoUpdate = 1;
        ResultadosUpdate.Clear();
    }

    /// <summary>Devuelve el orden que el servicio pidio, ya aplicado a una query vacia.</summary>
    public IReadOnlyList<T> OrdenEsperado()
    {
        if (UltimoOrden == null) return [];
        return UltimoOrden(_entidades.AsQueryable()).Select(e => e).ToList();
    }

    // ---------------------------------------------------------------- interfaz

    public Task<T> Add(T entity)
    {
        VecesAdd++;
        UltimaEntidadAgregada = entity;

        if (AlAdd != null)
            return Task.FromResult(AlAdd(entity)!);

        AsignarPkSiEsCero(entity);
        _entidades.Add(entity);
        return Task.FromResult(entity);
    }

    public Task AddRange(IEnumerable<T> entities)
    {
        var lista = entities.ToList();
        foreach (var e in lista)
        {
            VecesAdd++;
            UltimaEntidadAgregada = e;
            AsignarPkSiEsCero(e);
            _entidades.Add(e);
        }
        return Task.CompletedTask;
    }

    public Task<int> Update(T entity)
    {
        VecesUpdate++;
        UltimaEntidadActualizada = entity;

        var resultado = ResultadosUpdate.Count > 0 ? ResultadosUpdate.Dequeue() : ResultadoUpdate;

        // La entidad real de EF queda trackeada: se actualizan sus valores, que es
        // lo que hace el SaveChanges() de la implementacion de verdad. Si
        // SaveChanges no afecta ninguna fila, asi que no se escribe nada.
        if (resultado > 0) {
            var almacenada = BuscarEnAlmacen(ClaveDe(entity));
            if (almacenada != null && !ReferenceEquals(almacenada, entity))
                CopiarValores(entity, almacenada);
        }

        return Task.FromResult(resultado);
    }

    public Task Delete(T entity)
    {
        VecesDelete++;
        UltimaEntidadEliminada = entity;
        var almacenada = BuscarEnAlmacen(ClaveDe(entity));
        if (almacenada != null) _entidades.Remove(almacenada);
        else _entidades.Remove(entity);
        return Task.CompletedTask;
    }

    public Task DeleteRange(IEnumerable<T> entities)
    {
        foreach (var e in entities.ToList())
        {
            VecesDelete++;
            UltimaEntidadEliminada = e;
            var almacenada = BuscarEnAlmacen(ClaveDe(e));
            if (almacenada != null) _entidades.Remove(almacenada);
            else _entidades.Remove(e);
        }
        return Task.CompletedTask;
    }

    public Task<T?> GetById(int id)
    {
        VecesGetById++;
        if (_propiedadId == null)
            throw new InvalidOperationException($"{typeof(T).Name} no tiene una propiedad 'Id' de tipo int.");
        UltimoFiltro = null;
        return Task.FromResult(BuscarEnAlmacen(id));
    }

    public Task<IEnumerable<T?>> GetAll()
    {
        VecesGetAll++;
        return Task.FromResult<IEnumerable<T?>>(_entidades.Cast<T?>().ToList());
    }

    public Task<T> GetAsync(Expression<Func<T, bool>>? filtro = null, string incluir = "")
    {
        VecesGetAsync++;
        UltimoFiltro = filtro;
        UltimoInclude = incluir;

        if (AlGetAsync != null)
            return Task.FromResult(AlGetAsync(filtro)!);

        var query = _entidades.AsQueryable();
        if (filtro != null) query = query.Where(filtro);

        // AsNoTracking: se devuelve una copia.
        return Task.FromResult(query.FirstOrDefault() is { } primera ? Copia(primera) : default(T)!);
    }

    public Task<IEnumerable<T>> ListarAsync(
        Expression<Func<T, bool>>? filtro = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orden = null,
        string incluir = "")
    {
        VecesListar++;
        UltimoFiltro = filtro;
        UltimoOrden = orden;
        UltimoInclude = incluir;

        if (AlListarAsync != null)
            return Task.FromResult(AlListarAsync());

        IQueryable<T> query = _entidades.AsQueryable();
        if (filtro != null) query = query.Where(filtro);
        if (orden != null) query = orden(query);

        return Task.FromResult<IEnumerable<T>>(query.ToList());
    }

    // ---------------------------------------------------------------- internos

    private T? BuscarEnAlmacen(int? clave)
    {
        if (clave == null || _propiedadId == null) return null;
        return _entidades.FirstOrDefault(e => _propiedadId.GetValue(e) is int id && id == clave.Value);
    }

    private int? ClaveDe(T entity)
        => _propiedadId?.GetValue(entity) as int?;

    private void AsignarPkSiEsCero(T entity)
    {
        if (_propiedadId == null || _propiedadId.PropertyType != typeof(int)) return;
        if (_propiedadId.GetValue(entity) is int actual && actual != 0) return;
        _propiedadId.SetValue(entity, _siguienteId++);
    }

    /// <summary>Copia superficial: mismos valores, navegaciones compartidas.</summary>
    private static T Copia(T origen)
    {
        var destino = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
        CopiarValores(origen, destino);
        return destino;
    }

    private static void CopiarValores(T origen, T destino)
    {
        for (var tipo = origen.GetType(); tipo != null && tipo != typeof(object); tipo = tipo.BaseType)
        {
            foreach (var campo in tipo.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                campo.SetValue(destino, campo.GetValue(origen));
        }
    }
}
