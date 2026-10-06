using FluentAssertions;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Validadores;

/// <summary>
/// Tests de los validadores sincronos de <see cref="ClienteValidador"/> (#10):
/// <c>NormalizarYValidar</c>, <c>NormalizarYValidarPatch</c> y los validadores
/// sueltos de documento, nombre, correo y telefono.
/// </summary>
public class ClienteValidadorTests
{
    private static ClienteValidador CrearValidador(params Cliente[] clientes)
        => new(new RepositorioGenericoVentasFake<Cliente>(clientes));

    // ------------------------------------------------------- NormalizarYValidar

    [Fact]
    public void NormalizarYValidar_acepta_un_dto_valido()
    {
        var validador = CrearValidador();

        validador.NormalizarYValidar(ClienteDtoBuilder.Uno().Build());
    }

    [Fact]
    public void NormalizarYValidar_recorta_nombre_correo_y_telefono()
    {
        var validador = CrearValidador();
        var dto = ClienteDtoBuilder.Uno()
            .ConNombre("  Juan Perez  ")
            .ConCorreo("   juan@example.com   ")
            .ConTelefono("  3514445555  ")
            .Build();

        validador.NormalizarYValidar(dto);

        dto.Nombre.Should().Be("Juan Perez");
        dto.Correo.Should().Be("juan@example.com");
        dto.Telefono.Should().Be("3514445555");
    }

    [Fact]
    public void NormalizarYValidar_convierte_un_telefono_vacio_en_null()
    {
        var validador = CrearValidador();
        var dto = ClienteDtoBuilder.Uno().ConTelefono("   ").Build();

        validador.NormalizarYValidar(dto);

        dto.Telefono.Should().BeNull("la linea 39 normaliza el vacio a null");
    }

    [Fact]
    public void NormalizarYValidar_acepta_un_telefono_null()
    {
        var validador = CrearValidador();
        var dto = ClienteDtoBuilder.Uno().ConTelefono(null).Build();

        validador.NormalizarYValidar(dto);

        dto.Telefono.Should().BeNull();
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_dto_null()
    {
        CrearValidador().Invoking(v => v.NormalizarYValidar(null!))
            .Should().Throw<DataException>()
            .WithMessage("No se recibió ningún dato.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_documento_que_no_tiene_8_digitos()
    {
        var validador = CrearValidador();
        var dto = ClienteDtoBuilder.Uno().ConDocumento(123).Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El documento debe tener 8 dígitos.");
    }

    [Fact]
    public void NormalizarYValidar_rechaza_un_correo_con_formato_invalido()
    {
        var validador = CrearValidador();
        var dto = ClienteDtoBuilder.Uno().ConCorreo("no-es-un-correo").Build();

        Assert.Throws<DataException>(() => validador.NormalizarYValidar(dto))
            .Message.Should().Be("El correo no tiene un formato válido.");
    }

    // ------------------------------------------------------- NormalizarYValidarPatch

    [Fact]
    public void NormalizarYValidarPatch_acepta_un_patch_con_un_solo_campo()
    {
        var validador = CrearValidador();

        validador.NormalizarYValidarPatch(new ClientePatchDto { Nombre = "Nuevo Nombre" });
    }

    [Fact]
    public void NormalizarYValidarPatch_rechaza_un_patch_totalmente_vacio()
    {
        var validador = CrearValidador();

        var excepcion = Assert.Throws<DataException>(() => validador.NormalizarYValidarPatch(new ClientePatchDto()));

        excepcion.Message.Should().Be("No se recibieron datos para actualizar.");
    }

    [Fact]
    public void NormalizarYValidarPatch_rechaza_un_dto_null()
    {
        CrearValidador().Invoking(v => v.NormalizarYValidarPatch(null!))
            .Should().Throw<DataException>()
            .WithMessage("No se recibió ningún dato.");
    }

    [Fact]
    public void NormalizarYValidarPatch_acepta_solo_el_flag_Activo()
    {
        CrearValidador().NormalizarYValidarPatch(new ClientePatchDto { Activo = false });
    }

    [Fact]
    public void NormalizarYValidarPatch_acepta_activo_en_true_solo()
    {
        CrearValidador().NormalizarYValidarPatch(new ClientePatchDto { Activo = true });
    }

    [Fact]
    public void NormalizarYValidarPatch_recorta_los_campos_de_texto_que_vienen_con_espacios()
    {
        var validador = CrearValidador();
        var patch = new ClientePatchDto {
            Nombre = "  Juan Perez  ",
            Correo = "  juan@example.com  ",
            Telefono = "  3514445555  "
        };

        validador.NormalizarYValidarPatch(patch);

        patch.Nombre.Should().Be("Juan Perez");
        patch.Correo.Should().Be("juan@example.com");
        patch.Telefono.Should().Be("3514445555");
    }

    [Fact]
    public void NormalizarYValidarPatch_convierte_un_telefono_vacio_en_null()
    {
        var validador = CrearValidador();
        var patch = new ClientePatchDto { Telefono = "   " };

        validador.NormalizarYValidarPatch(patch);

        patch.Telefono.Should().BeNull();
    }

    [Fact]
    public void NormalizarYValidarPatch_rechaza_un_nombre_invalido()
    {
        var validador = CrearValidador();

        Assert.Throws<DataException>(() => validador.NormalizarYValidarPatch(new ClientePatchDto { Nombre = "Ab" }))
            .Message.Should().Be("El nombre debe tener entre 4 y 100 caracteres.");
    }

    [Fact]
    public void NormalizarYValidarPatch_rechaza_un_correo_invalido()
    {
        var validador = CrearValidador();

        Assert.Throws<DataException>(() => validador.NormalizarYValidarPatch(new ClientePatchDto { Correo = "arroba" }))
            .Message.Should().Be("El correo no tiene un formato válido.");
    }

    [Fact]
    public void NormalizarYValidarPatch_rechaza_un_telefono_de_mas_de_20_caracteres()
    {
        var validador = CrearValidador();

        Assert.Throws<DataException>(() => validador.NormalizarYValidarPatch(new ClientePatchDto { Telefono = new string('9', 21) }))
            .Message.Should().Be("El teléfono no puede tener más de 20 caracteres.");
    }

    // ------------------------------------------------------- ValidarDocumento

    [Theory]
    [InlineData(ClienteValidador.DocumentoMinimo)]
    [InlineData(30111222)]
    [InlineData(ClienteValidador.DocumentoMaximo)]
    public void ValidarDocumento_acepta_valores_de_8_digitos(int documento)
    {
        CrearValidador().ValidarDocumento(documento);
    }

    [Theory]
    [InlineData(ClienteValidador.DocumentoMinimo - 1)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ClienteValidador.DocumentoMaximo + 1)]
    public void ValidarDocumento_rechaza_valores_fuera_del_rango_de_8_digitos(int documento)
    {
        CrearValidador().Invoking(v => v.ValidarDocumento(documento))
            .Should().Throw<DataException>()
            .WithMessage("El documento debe tener 8 dígitos.");
    }

    // ------------------------------------------------------- ValidarNombre

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public void ValidarNombre_rechaza_un_nombre_vacio_o_solo_espacios(string nombre)
    {
        CrearValidador().Invoking(v => v.ValidarNombre(nombre))
            .Should().Throw<DataException>()
            .WithMessage("El nombre es requerido.");
    }

    [Theory]
    [InlineData("Abc")]
    public void ValidarNombre_rechaza_un_nombre_mas_corto_de_4_caracteres(string nombre)
    {
        CrearValidador().Invoking(v => v.ValidarNombre(nombre))
            .Should().Throw<DataException>()
            .WithMessage("El nombre debe tener entre 4 y 100 caracteres.");
    }

    [Fact]
    public void ValidarNombre_rechaza_un_nombre_de_101_caracteres()
    {
        CrearValidador().Invoking(v => v.ValidarNombre(new string('a', 101)))
            .Should().Throw<DataException>()
            .WithMessage("El nombre debe tener entre 4 y 100 caracteres.");
    }

    [Theory]
    [InlineData("Juan")]
    public void ValidarNombre_acepta_el_minimo_del_rango(string nombre)
    {
        CrearValidador().ValidarNombre(nombre);
    }

    [Fact]
    public void ValidarNombre_acepta_exactamente_100_caracteres()
    {
        CrearValidador().ValidarNombre(new string('a', 100));
    }

    // ------------------------------------------------------- ValidarCorreo

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public void ValidarCorreo_rechaza_un_correo_vacio_o_solo_espacios(string correo)
    {
        CrearValidador().Invoking(v => v.ValidarCorreo(correo))
            .Should().Throw<DataException>()
            .WithMessage("El correo es requerido.");
    }

    [Theory]
    [InlineData("a@b")]
    public void ValidarCorreo_rechaza_un_correo_mas_corto_de_5_caracteres(string correo)
    {
        CrearValidador().Invoking(v => v.ValidarCorreo(correo))
            .Should().Throw<DataException>()
            .WithMessage("El correo debe tener entre 5 y 150 caracteres.");
    }

    [Fact]
    public void ValidarCorreo_rechaza_un_correo_de_151_caracteres()
    {
        CrearValidador().Invoking(v => v.ValidarCorreo(new string('a', 151)))
            .Should().Throw<DataException>()
            .WithMessage("El correo debe tener entre 5 y 150 caracteres.");
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("@example.com")]
    [InlineData("doble@@example.com")]
    public void ValidarCorreo_rechaza_un_correo_con_formato_invalido(string correo)
    {
        CrearValidador().Invoking(v => v.ValidarCorreo(correo))
            .Should().Throw<DataException>()
            .WithMessage("El correo no tiene un formato válido.");
    }

    /// <summary>
    /// Caracteriza el comportamiento de <see cref="EmailAddressAttribute"/> en .NET 9:
    /// es <b>permisivo con espacios</b> dentro de la parte local (relajacion de .NET 7+),
    /// asi que "Juan Perez@ejemplo.com" pasa la validacion de formato. Solo el chequeo
    /// de longitud y el dominio loarian.
    /// Referencia: #10.
    /// </summary>
    [Fact]
    public void ValidarCorreo_acepta_espacios_en_la_parte_local_por_comportamiento_del_framework()
    {
        CrearValidador().ValidarCorreo("juan perez@example.com");
    }

    [Fact]
    public void ValidarCorreo_acepta_un_correo_de_exactamente_5_caracteres()
    {
        CrearValidador().ValidarCorreo("a@b.c");
    }

    [Fact]
    public void ValidarCorreo_rechaza_un_correo_de_4_caracteres_utiles_tras_recortar()
    {
        CrearValidador().Invoking(v => v.ValidarCorreo("  a@b  "))
            .Should().Throw<DataException>()
            .WithMessage("El correo debe tener entre 5 y 150 caracteres.");
    }

    [Fact]
    public void ValidarCorreo_mide_la_longitud_sobre_el_correo_ya_recortado()
    {
        // 5 caracteres utiles rodeados de espacios: recortado queda en 5, el minimo valido.
        CrearValidador().ValidarCorreo("  a@b.c  ");
    }

    [Fact]
    public void ValidarCorreo_acepta_un_correo_de_exactamente_150_caracteres()
    {
        var correo = $"{new string('a', 138)}@example.com";
        correo.Length.Should().Be(150);

        CrearValidador().ValidarCorreo(correo);
    }

    [Fact]
    public void ValidarCorreo_rechaza_un_correo_de_151_caracteres_por_longitud()
    {
        var correo = $"{new string('a', 139)}@example.com";
        correo.Length.Should().Be(151);

        CrearValidador().Invoking(v => v.ValidarCorreo(correo))
            .Should().Throw<DataException>()
            .WithMessage("El correo debe tener entre 5 y 150 caracteres.");
    }

    // ------------------------------------------------------- ValidarTelefono

    [Fact]
    public void ValidarTelefono_acepta_null()
    {
        CrearValidador().ValidarTelefono(null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("3514445555")]
    [InlineData("+54 9 351 444 5555")]
    public void ValidarTelefono_acepta_hasta_20_caracteres(string telefono)
    {
        CrearValidador().ValidarTelefono(telefono);
    }

    [Fact]
    public void ValidarTelefono_rechaza_mas_de_20_caracteres()
    {
        CrearValidador().Invoking(v => v.ValidarTelefono(new string('9', 21)))
            .Should().Throw<DataException>()
            .WithMessage("El teléfono no puede tener más de 20 caracteres.");
    }

    // ------------------------------------------------------- constructor

    [Fact]
    public void El_constructor_rechaza_un_repositorio_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ClienteValidador(null!));
    }
}