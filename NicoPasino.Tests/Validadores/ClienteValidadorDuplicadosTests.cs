using FluentAssertions;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Validadores;

/// <summary>
/// Tests de unicidad de <see cref="ClienteValidador"/> (#11). <c>documento</c> y
/// <c>correo</c> tienen indice unico en la BD, asi que el validador consulta antes
/// de insertar. El fake deja verificar ademas el <b>parametro <c>idExcluir</c></b>,
/// que es lo que permite que un Patch no choque consigo mismo.
/// </summary>
public class ClienteValidadorDuplicadosTests
{
    private static Cliente Existente(int id, int documento, string correo)
        => ClienteBuilder.Uno().ConId(id).ConDocumento(documento).ConCorreo(correo).Build();

    private static (ClienteValidador Validador, RepositorioGenericoVentasFake<Cliente> Repo)
        CrearCon(params Cliente[] clientes)
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>(clientes);
        return (new ClienteValidador(repo), repo);
    }

    // ------------------------------------------------------- documento unico

    [Fact]
    public async Task ValidarDocumentoUnicoAsync_acepta_un_documento_que_no_existe()
    {
        var (validador, _) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        await validador.ValidarDocumentoUnicoAsync(40998877);
    }

    [Fact]
    public async Task ValidarDocumentoUnicoAsync_rechaza_un_documento_ya_registrado()
    {
        var (validador, _) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarDocumentoUnicoAsync(30111222));

        excepcion.Message.Should().Be("Ya existe un cliente con el documento '30111222'.");
    }

    [Fact]
    public async Task ValidarDocumentoUnicoAsync_con_idExcluir_permite_actualizar_el_mismo_cliente()
    {
        var (validador, _) = CrearCon(Existente(7, 30111222, "siete@example.com"));

        await validador.ValidarDocumentoUnicoAsync(30111222, idExcluir: 7);
    }

    [Fact]
    public async Task ValidarDocumentoUnicoAsync_con_idExcluir_igual_a_otro_cliente_sigue_chocando()
    {
        var (validador, _) = CrearCon(
            Existente(7, 30111222, "siete@example.com"),
            Existente(8, 30111222, "otro@example.com")
        );

        await Assert.ThrowsAsync<DataException>(() => validador.ValidarDocumentoUnicoAsync(30111222, idExcluir: 7));
    }

    [Fact]
    public async Task ValidarDocumentoUnicoAsync_sin_idExcluir_choca_incluso_contra_el_mismo_id()
    {
        var (validador, _) = CrearCon(Existente(7, 30111222, "siete@example.com"));

        await Assert.ThrowsAsync<DataException>(() => validador.ValidarDocumentoUnicoAsync(30111222));
    }

    [Fact]
    public async Task ValidarDocumentoUnicoAsync_acepta_el_documento_si_el_repo_esta_vacio()
    {
        var (validador, repo) = CrearCon();

        await validador.ValidarDocumentoUnicoAsync(30111222);

        repo.VecesGetAsync.Should().Be(1);
    }

    [Fact]
    public async Task ValidarDocumentoUnicoAsync_consulta_una_sola_vez_el_repo()
    {
        var (validador, repo) = CrearCon();

        await validador.ValidarDocumentoUnicoAsync(30111222);

        repo.VecesGetAsync.Should().Be(1);
        repo.UltimoFiltro.Should().NotBeNull();
    }

    // ------------------------------------------------------- correo unico

    [Fact]
    public async Task ValidarCorreoUnicoAsync_acepta_un_correo_que_no_existe()
    {
        var (validador, _) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        await validador.ValidarCorreoUnicoAsync("nuevo@example.com");
    }

    [Fact]
    public async Task ValidarCorreoUnicoAsync_rechaza_un_correo_ya_registrado()
    {
        var (validador, _) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarCorreoUnicoAsync("uno@example.com"));

        excepcion.Message.Should().Be("Ya existe un cliente con el correo 'uno@example.com'.");
    }

    [Fact]
    public async Task ValidarCorreoUnicoAsync_ignora_un_correo_vacio_o_en_blanco()
    {
        var (validador, repo) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        await validador.ValidarCorreoUnicoAsync("");
        await validador.ValidarCorreoUnicoAsync("   ");

        repo.VecesGetAsync.Should().Be(0, "la linea 84 corta antes de consultar");
    }

    [Fact]
    public async Task ValidarCorreoUnicoAsync_con_idExcluir_permite_actualizar_el_mismo_cliente()
    {
        var (validador, _) = CrearCon(Existente(7, 30111222, "siete@example.com"));

        await validador.ValidarCorreoUnicoAsync("siete@example.com", idExcluir: 7);
    }

    [Fact]
    public async Task ValidarCorreoUnicoAsync_compara_contra_el_correo_ya_recortado()
    {
        var (validador, _) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        // Sin Trim() en el filtro no encontraria el duplicado y dejaria pasar el alta.
        await Assert.ThrowsAsync<DataException>(() => validador.ValidarCorreoUnicoAsync("  uno@example.com  "));
    }

    // ------------------------------------------------------- ValidarDuplicadosAsync

    [Fact]
    public async Task ValidarDuplicadosAsync_acepta_un_cliente_nuevo()
    {
        var (validador, _) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        await validador.ValidarDuplicadosAsync(ClienteDtoBuilder.Uno()
            .ConDocumento(40998877)
            .ConCorreo("nuevo@example.com")
            .Build());
    }

    [Fact]
    public async Task ValidarDuplicadosAsync_valida_el_documento_primero_y_aborta_antes_del_correo()
    {
        var (validador, repo) = CrearCon(Existente(1, 30111222, "nuevo@example.com"));

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarDuplicadosAsync(
            ClienteDtoBuilder.Uno().ConDocumento(30111222).ConCorreo("nuevo@example.com").Build()
        ));

        excepcion.Message.Should().Be("Ya existe un cliente con el documento '30111222'.");
        repo.VecesGetAsync.Should().Be(1, "el documento se valida primero (linea 70)");
    }

    [Fact]
    public async Task ValidarDuplicadosAsync_llega_al_correo_si_el_documento_no_choca()
    {
        var (validador, repo) = CrearCon(Existente(1, 30111222, "otro@example.com"));

        await Assert.ThrowsAsync<DataException>(() => validador.ValidarDuplicadosAsync(
            ClienteDtoBuilder.Uno().ConDocumento(40998877).ConCorreo("otro@example.com").Build()
        ));

        repo.VecesGetAsync.Should().Be(2, "documento ok, luego correo");
    }

    [Fact]
    public async Task ValidarDuplicadosAsync_hace_las_dos_consultas_si_no_hay_duplicados()
    {
        var (validador, repo) = CrearCon();

        await validador.ValidarDuplicadosAsync(ClienteDtoBuilder.Uno().Build());

        repo.VecesGetAsync.Should().Be(2);
    }

    [Fact]
    public async Task ValidarDuplicadosAsync_rechaza_un_cliente_duplicado_completo()
    {
        var (validador, _) = CrearCon(Existente(1, 30111222, "uno@example.com"));

        var excepcion = await Assert.ThrowsAsync<DataException>(() => validador.ValidarDuplicadosAsync(
            ClienteDtoBuilder.Uno().ConDocumento(30111222).ConCorreo("uno@example.com").Build()
        ));

        excepcion.Message.Should().StartWith("Ya existe un cliente con");
    }

    /// <summary>
    /// <c>ValidarDuplicadosAsync</c> con el mismo <c>idExcluir</c> para documento y
    /// correo: un cliente puede cambiar sus datos y volver a validarse sin chocar
    /// consigo mismo.
    /// </summary>
    [Fact]
    public async Task ValidarDuplicadosAsync_con_el_propio_id_no_choca_contra_si_mismo()
    {
        var (validador, repo) = CrearCon(Existente(7, 30111222, "siete@example.com"));
        var dto = ClienteDtoBuilder.Uno()
            .ConDocumento(30111222)
            .ConCorreo("siete@example.com")
            .Build();

        await validador.ValidarDuplicadosAsync(dto, idExcluir: 7);

        repo.VecesGetAsync.Should().Be(2, "las dos consultas se hacen, ninguna encuentra duplicado");
    }

    [Fact]
    public async Task ValidarDuplicadosAsync_sin_idExcluir_choca_contra_el_mismo_registro()
    {
        var (validador, _) = CrearCon(Existente(7, 30111222, "siete@example.com"));
        var dto = ClienteDtoBuilder.Uno()
            .ConDocumento(30111222)
            .ConCorreo("siete@example.com")
            .Build();

        await Assert.ThrowsAsync<DataException>(() => validador.ValidarDuplicadosAsync(dto));
    }
}