using System.Runtime.CompilerServices;
using NicoPasino.Core.Mapper;

namespace NicoPasino.Tests.Common;

/// <summary>
/// Carga la configuracion global de Mapster <b>una sola vez por ensamblado</b>,
/// antes de que se ejecute cualquier test.
/// <para>
/// <c>TypeAdapterConfig</c> es estado estatico global. En produccion se inicializa
/// en <c>Program.Main</c> (<c>Program.cs:32</c>). Si un test llama a
/// <c>Adapt&lt;&gt;()</c> sin la config cargada, come <c>NullReferenceException</c>
/// (por ejemplo <c>ProductoDto.Categoria</c> desreferencia
/// <c>IdCategoriaNavigation</c>).
/// </para>
/// <para>
/// xUnit ejecuta las colecciones <b>en paralelo</b>, asi que llamar la config en
/// cada constructor seria una bomba de reloja.
/// <c>ModuleInitializer</c> corre una unica vez, antes del <c>Main</c> de los tests.
/// </para>
/// </summary>
internal static class InicializadorMapster
{
    [ModuleInitializer]
    internal static void Inicializar()
        => MappingConfig.VentasMappings();
}
