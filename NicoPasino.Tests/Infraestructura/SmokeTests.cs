using FluentAssertions;
using Mapster;
using NicoPasino.Tests.Common.Builders;

namespace NicoPasino.Tests.Infraestructura;

/// <summary>
/// Test de humo del proyecto (#4): confirma que el runner, los 4 ProjectReference,
/// las referencias a los paquetes y el <c>ModuleInitializer</c> de Mapster funcionan.
/// Si esto falla, el problema es de configuracion del proyecto, no de codigo de negocio.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void El_proyecto_referencia_los_cuatro_proyectos_de_produccion()
    {
        typeof(NicoPasino.Servicios.Validaciones.ProductoValidador).Assembly.GetName().Name
            .Should().Be("NicoPasino.Servicios");
        typeof(NicoPasino.Core.DTO.Ventas.ProductoDto).Assembly.GetName().Name
            .Should().Be("NicoPasino.Core");
        typeof(NicoPasino.Infra.Data.ventasdbContext).Assembly.GetName().Name
            .Should().Be("NicoPasino.Infra");
        typeof(Program).Assembly.GetName().Name
            .Should().Be("NicoPasino");
    }

    [Fact]
    public void El_modulo_inicializador_cargo_la_config_global_de_Mapster()
    {
        // Si el ModuleInitializer no corrio, esta linea explota con
        // NullReferenceException sobre IdCategoriaNavigation.
        var producto = new ProductoBuilder().Build();
        var dto = producto.Adapt<NicoPasino.Core.DTO.Ventas.ProductoDto>();

        dto.Nombre.Should().Be("Gaseosa 500ml");
        dto.Categoria.Should().Be("Bebidas");
    }

    [Fact]
    public void Los_builders_generan_entidades_validas_por_defecto()
    {
        new ProductoBuilder().Build().Activo.Should().BeTrue();
        new ClienteBuilder().Build().Documento.Should().BeInRange(10_000_000, 99_999_999);
        new CategoriaBuilder().Build().Nombre.Should().NotBeNullOrWhiteSpace();
        new VentaBuilder().ConClientePorDefecto().Build().Ventaporproducto.Should().BeEmpty();
    }
}