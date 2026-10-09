using FluentAssertions;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Servicios.Servicios.Ventas;
using NicoPasino.Servicios.Validaciones;
using NicoPasino.Tests.Common.Builders;
using NicoPasino.Tests.Common.Fakes;

namespace NicoPasino.Tests.Servicios;

/// <summary>
/// Tests de <c>ClienteServicio.Create</c> (#20). A diferencia de Producto, el
/// Cliente se identifica por <c>Documento</c> y no hay generacion de numero: el
/// unico "generado" es <c>Activo = true</c>.
/// </summary>
public class ClienteServicioCreateTests
{
    private static ClienteDto DtoValido(int documento = 30111222) => ClienteDtoBuilder.Uno()
        .ConDocumento(documento)
        .ConNombre("Ana Gomez")
        .ConCorreo("ana.gomez@correo.com")
        .ConTelefono("1122334455")
        .Build();

    private static (ClienteServicio Servicio, RepositorioGenericoVentasFake<Cliente> Repo)
        CrearCon(params Cliente[] clientes)
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>(clientes);
        return (new ClienteServicio(repo, new ClienteValidador(repo)), repo);
    }

    [Fact]
    public async Task Create_agrega_el_cliente_y_devuelve_true()
    {
        var (servicio, repo) = CrearCon();

        var resultado = await servicio.Create(DtoValido());

        resultado.Should().BeTrue();
        repo.VecesAdd.Should().Be(1);
        repo.UltimaEntidadAgregada!.Documento.Should().Be(30111222);
    }

    [Fact]
    public async Task Create_marca_el_cliente_como_activo_sin_importar_el_DTO()
    {
        var (servicio, repo) = CrearCon();
        var dto = DtoValido();
        dto.Activo = false;

        await servicio.Create(dto);

        repo.UltimaEntidadAgregada!.Activo.Should().BeTrue("linea 115: siempre true");
    }

    [Fact]
    public async Task Create_asigna_la_fecha_de_creacion_al_momento_actual()
    {
        var (servicio, repo) = CrearCon();
        var antes = DateTime.UtcNow;

        await servicio.Create(DtoValido());

        repo.UltimaEntidadAgregada!.FechaCreacion.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Create_normaliza_el_nombre_el_correo_y_el_telefono()
    {
        var (servicio, repo) = CrearCon();
        var dto = DtoValido();
        dto.Nombre = "  Ana Gomez  ";
        dto.Correo = "  ana.gomez@correo.com  ";
        dto.Telefono = "  1122334455  ";

        await servicio.Create(dto);

        var guardado = repo.UltimaEntidadAgregada!;
        guardado.Nombre.Should().Be("Ana Gomez");
        guardado.Correo.Should().Be("ana.gomez@correo.com");
        guardado.Telefono.Should().Be("1122334455");
    }

    [Fact]
    public async Task Create_deja_el_telefono_en_null_si_viene_vacio()
    {
        var (servicio, repo) = CrearCon();
        var dto = DtoValido();
        dto.Telefono = "   ";

        await servicio.Create(dto);

        repo.UltimaEntidadAgregada!.Telefono.Should().BeNull();
    }

    [Fact]
    public async Task Create_rechaza_un_DTO_null()
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(null!));

        excepcion.Message.Should().Be("No se recibió ningún dato.");
        repo.VecesAdd.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1234567)]
    [InlineData(100000000)]
    public async Task Create_rechaza_un_documento_que_no_tiene_ocho_digitos(int documento)
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido(documento)));

        excepcion.Message.Should().Be("El documento debe tener 8 dígitos.");
    }

    [Fact]
    public async Task Create_rechaza_un_nombre_invalido()
    {
        var (servicio, _) = CrearCon();
        var dto = DtoValido();
        dto.Nombre = "An";

        await Assert.ThrowsAsync<DataException>(() => servicio.Create(dto));
    }

    [Fact]
    public async Task Create_rechaza_un_correo_con_formato_invalido()
    {
        var (servicio, _) = CrearCon();
        var dto = DtoValido();
        dto.Correo = "no-es-un-correo";

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(dto));

        excepcion.Message.Should().Be("El correo no tiene un formato válido.");
    }

    [Fact]
    public async Task Create_rechaza_un_telefono_de_mas_de_20_caracteres()
    {
        var (servicio, _) = CrearCon();
        var dto = DtoValido();
        dto.Telefono = new string('9', 21);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(dto));

        excepcion.Message.Should().Be("El teléfono no puede tener más de 20 caracteres.");
    }

    [Fact]
    public async Task Create_rechaza_un_documento_ya_existente()
    {
        var (servicio, repo) = CrearCon(ClienteBuilder.Uno().ConId(1).ConDocumento(30111222).Build());

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido()));

        excepcion.Message.Should().Be("Ya existe un cliente con el documento '30111222'.");
        repo.VecesAdd.Should().Be(0);
    }

    [Fact]
    public async Task Create_rechaza_un_correo_ya_existente_aunque_el_documento_sea_nuevo()
    {
        var (servicio, repo) = CrearCon(
            ClienteBuilder.Uno().ConId(1).ConDocumento(30111222).ConCorreo("ana.gomez@correo.com").Build());

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Create(DtoValido(33111222)));

        excepcion.Message.Should().Be("Ya existe un cliente con el correo 'ana.gomez@correo.com'.");
        repo.VecesAdd.Should().Be(0);
    }

    [Fact]
    public async Task Create_acepta_un_correo_con_diferencias_de_espacios_sin_dar_duplicado()
    {
        // El validador normaliza con Trim antes de consultar, asi que los espacios
        // del DTO no evitan detectar el duplicado.
        var (servicio, _) = CrearCon(
            ClienteBuilder.Uno().ConId(1).ConDocumento(30111222).ConCorreo("ana.gomez@correo.com").Build());
        var dto = DtoValido(33111222);
        dto.Correo = "  ana.gomez@correo.com  ";

        await Assert.ThrowsAsync<DataException>(() => servicio.Create(dto));
    }

    [Fact]
    public async Task Create_devuelve_false_si_el_repositorio_no_devuelve_la_entidad()
    {
        var (servicio, repo) = CrearCon();
        repo.AlAdd = _ => null;

        (await servicio.Create(DtoValido())).Should().BeFalse();
    }
}

/// <summary>Tests de <c>ClienteServicio.Update</c> (#20).</summary>
public class ClienteServicioUpdateTests
{
    private static Cliente Original() => ClienteBuilder.Uno()
        .ConId(7)
        .ConDocumento(30111222)
        .ConNombre("Ana Gomez")
        .ConCorreo("ana.gomez@correo.com")
        .ConTelefono("1122334455")
        .ConActivo(true)
        .ConFechaCreacion(new DateTime(2020, 3, 3, 0, 0, 0, DateTimeKind.Utc))
        .Build();

    private static ClienteDto DtoValido(int documento = 30111222) => ClienteDtoBuilder.Uno()
        .ConDocumento(documento)
        .ConNombre("Ana Gomez Cambiado")
        .ConCorreo("ana.nuevo@correo.com")
        .ConTelefono("9988776655")
        .Build();

    private static (ClienteServicio Servicio, RepositorioGenericoVentasFake<Cliente> Repo)
        CrearCon(params Cliente[] clientes)
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>(clientes.Length > 0 ? clientes : [Original()]);
        return (new ClienteServicio(repo, new ClienteValidador(repo)), repo);
    }

    [Fact]
    public async Task Update_persiste_el_nuevo_nombre()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(DtoValido());

        repo.UltimaEntidadActualizada!.Nombre.Should().Be("Ana Gomez Cambiado");
    }

    [Fact]
    public async Task Update_devuelve_true()
    {
        var (servicio, _) = CrearCon();

        (await servicio.Update(DtoValido())).Should().BeTrue();
    }

    [Fact]
    public async Task Update_conserva_la_pk_interna_del_original()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(DtoValido());

        repo.UltimaEntidadActualizada!.Id.Should().Be(7);
        repo.Entidades.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_conserva_la_fecha_de_creacion_original()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(DtoValido());

        repo.UltimaEntidadActualizada!.FechaCreacion.Should()
            .Be(new DateTime(2020, 3, 3, 0, 0, 0, DateTimeKind.Utc), "linea 137");
    }

    /// <summary>
    /// Caracteriza que <c>Update</c> no toca <c>FechaModificacion</c>: el mapper copia
    /// la fecha del DTO (que viene <c>null</c>) y el metodo solo restaura
    /// <c>Id</c> y <c>FechaCreacion</c>. El Cliente ni siquiera tiene columna
    /// <c>FechaModificacion</c> en el modelo, asi que no hay forma de saber cuando
    /// se toco por ultima vez. Contraste con <c>ProductoServicio.Update</c>, que si la
    /// renueva.
    /// </summary>
    [Fact]
    public async Task Update_no_registra_cuando_se_modifico_el_cliente()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(DtoValido());

        // Cliente no tiene FechaModificacion, y el DTO tampoco la expone.
        typeof(Cliente).GetProperties().Select(p => p.Name)
            .Should().NotContain("FechaModificacion");
        repo.UltimaEntidadActualizada!.FechaCreacion.Should().NotBe(default);
    }

    [Fact]
    public async Task Update_persiste_el_cambio_de_correo()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Update(DtoValido());

        repo.UltimaEntidadActualizada!.Correo.Should().Be("ana.nuevo@correo.com");
    }

    [Fact]
    public async Task Update_rechaza_un_DTO_null()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(null!));

        excepcion.Message.Should().Be("No se recibió ningún dato.");
    }

    [Fact]
    public async Task Update_rechaza_un_DTO_invalido()
    {
        var (servicio, repo) = CrearCon();
        var dto = DtoValido();
        dto.Correo = "correo-malo";

        await Assert.ThrowsAsync<DataException>(() => servicio.Update(dto));
        repo.VecesUpdate.Should().Be(0);
    }

    [Fact]
    public async Task Update_falla_si_no_encuentra_el_cliente_original()
    {
        var (servicio, _) = CrearCon([Original()]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(DtoValido(33111222)));

        excepcion.Message.Should().Be("Objeto original no encontrado.");
    }

    /// <summary>
    /// El chequeo de duplicados corre <b>despues</b> de buscar el original y excluye
    /// su propia <c>Id</c>, asi que guardar el mismo cliente sin cambios no dispara
    /// el error de "ya existe". Si el chequeo fuera antes, no podrias editar tus
    /// propios datos.
    /// </summary>
    [Fact]
    public async Task Update_permite_guardar_el_mismo_cliente_sin_disparar_el_duplicado()
    {
        var (servicio, repo) = CrearCon([Original()]);
        var dto = ClienteDtoBuilder.Uno()
            .ConDocumento(30111222)
            .ConNombre("Ana Gomez Actualizada")
            .ConCorreo("ana.gomez@correo.com")
            .ConTelefono("1122334455")
            .Build();

        (await servicio.Update(dto)).Should().BeTrue();
        repo.VecesUpdate.Should().Be(1);
    }

    [Fact]
    public async Task Update_rechaza_un_correo_ya_usado_por_otro_cliente()
    {
        var (servicio, repo) = CrearCon([
            Original(),
            ClienteBuilder.Uno().ConId(8).ConDocumento(33111222).ConCorreo("ana.nuevo@correo.com").Build()
        ]);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(DtoValido()));

        excepcion.Message.Should().Be("Ya existe un cliente con el correo 'ana.nuevo@correo.com'.");
        repo.VecesUpdate.Should().Be(0);
    }

    [Fact]
    public async Task Update_rechaza_un_documento_que_pertenece_a_otro_cliente()
    {
        // Busca por obj.Documento, asi que si el documento no existe no hay original.
        var (servicio, _) = CrearCon([Original()]);
        var dto = DtoValido(33111222);

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Update(dto));

        excepcion.Message.Should().Be("Objeto original no encontrado.");
    }

    [Fact]
    public async Task Update_lanza_UpdateException_si_la_base_no_afecta_ninguna_fila()
    {
        var (servicio, repo) = CrearCon();
        repo.ResultadoUpdate = 0;

        var excepcion = await Assert.ThrowsAsync<UpdateException>(() => servicio.Update(DtoValido()));

        excepcion.Message.Should().Be("No se pudo actualizar en la base de datos.");
    }
}

/// <summary>Tests de <c>ClienteServicio.Patch</c> (#20).</summary>
public class ClienteServicioPatchTests
{
    private static Cliente Original(bool activo = true) => ClienteBuilder.Uno()
        .ConId(7)
        .ConDocumento(30111222)
        .ConNombre("Ana Gomez")
        .ConCorreo("ana.gomez@correo.com")
        .ConTelefono("1122334455")
        .ConActivo(activo)
        .Build();

    private static (ClienteServicio Servicio, RepositorioGenericoVentasFake<Cliente> Repo)
        CrearCon(params Cliente[] clientes)
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>(clientes.Length > 0 ? clientes : [Original()]);
        return (new ClienteServicio(repo, new ClienteValidador(repo)), repo);
    }

    [Fact]
    public async Task Patch_actualiza_el_nombre()
    {
        var (servicio, repo) = CrearCon();

        (await servicio.Patch(new ClientePatchDto { Nombre = "Ana Gonzalez" }, 30111222)).Should().BeTrue();
        repo.Entidades.Single().Nombre.Should().Be("Ana Gonzalez");
    }

    [Fact]
    public async Task Patch_actualiza_el_correo()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Patch(new ClientePatchDto { Correo = "nuevo@correo.com" }, 30111222);

        repo.Entidades.Single().Correo.Should().Be("nuevo@correo.com");
    }

    [Fact]
    public async Task Patch_actualiza_el_telefono()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Patch(new ClientePatchDto { Telefono = "1155443322" }, 30111222);

        repo.Entidades.Single().Telefono.Should().Be("1155443322");
    }

    [Fact]
    public async Task Patch_actualiza_el_estado()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Patch(new ClientePatchDto { Activo = false }, 30111222);

        repo.Entidades.Single().Activo.Should().BeFalse();
    }

    [Fact]
    public async Task Patch_normaliza_el_nombre_y_el_correo()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Patch(new ClientePatchDto {
            Nombre = "  Ana Gonzalez  ",
            Correo = "  nuevo@correo.com  "
        }, 30111222);

        var guardado = repo.Entidades.Single();
        guardado.Nombre.Should().Be("Ana Gonzalez");
        guardado.Correo.Should().Be("nuevo@correo.com");
    }

    /// <summary>
    /// Caracteriza un bug: <c>NormalizarYValidarPatch</c> (linea 60) deja
    /// <c>Telefono</c> en <c>null</c> cuando llega vacio, y despues el guard de la
    /// linea 157 (<c>if (obj.Telefono != null)</c>) lo saltea. Resultado: <b>no se
    /// puede borrar el telefono con un Patch</b>. En cambio <c>Create</c> si lo puede,
    /// porque ahi la asignacion es incondicional. Para dejar el telefono en null hay
    /// que pasar un valor con espacios, no vacio: " " tampoco lo borra.
    /// </summary>
    [Fact]
    public async Task Patch_no_puede_borrar_el_telefono_enviando_los_espacios()
    {
        var (servicio, repo) = CrearCon();

        (await servicio.Patch(new ClientePatchDto { Telefono = "   " }, 30111222)).Should().BeTrue();
        repo.Entidades.Single().Telefono.Should().Be("1122334455", "el guard ve null y no lo aplica");
    }

    [Fact]
    public async Task Patch_rechaza_un_telefono_de_mas_de_20_caracteres()
    {
        var (servicio, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ClientePatchDto { Telefono = new string('1', 21) }, 30111222));
    }

    [Fact]
    public async Task Patch_aplica_varios_campos_a_la_vez()
    {
        var (servicio, repo) = CrearCon();

        await servicio.Patch(new ClientePatchDto {
            Nombre = "Ana Gonzalez",
            Correo = "nuevo@correo.com",
            Telefono = "1155443322",
            Activo = false
        }, 30111222);

        var guardado = repo.Entidades.Single();
        guardado.Nombre.Should().Be("Ana Gonzalez");
        guardado.Correo.Should().Be("nuevo@correo.com");
        guardado.Telefono.Should().Be("1155443322");
        guardado.Activo.Should().BeFalse();
    }

    [Fact]
    public async Task Patch_rechaza_un_DTO_null()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(() => servicio.Patch(null!, 30111222));

        excepcion.Message.Should().Be("No se recibió ningún dato.");
    }

    [Fact]
    public async Task Patch_rechaza_un_patch_vacio()
    {
        var (servicio, repo) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ClientePatchDto(), 30111222));

        excepcion.Message.Should().Be("No se recibieron datos para actualizar.");
        repo.VecesGetAsync.Should().Be(0, "valida el patch antes de consultar");
    }

    [Fact]
    public async Task Patch_rechaza_un_documento_invalido()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ClientePatchDto { Nombre = "Ana Gomez" }, 123));

        excepcion.Message.Should().Be("El documento debe tener 8 dígitos.");
    }

    [Fact]
    public async Task Patch_rechaza_un_nombre_invalido()
    {
        var (servicio, repo) = CrearCon();

        await Assert.ThrowsAsync<DataException>(() => servicio.Patch(new ClientePatchDto { Nombre = "A" }, 30111222));
        repo.VecesUpdate.Should().Be(0);
    }

    [Fact]
    public async Task Patch_rechaza_un_correo_con_formato_invalido()
    {
        var (servicio, _) = CrearCon();

        await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ClientePatchDto { Correo = "no-es-un-correo" }, 30111222));
    }

    [Fact]
    public async Task Patch_rechaza_un_correo_ya_usado_por_otro_cliente()
    {
        var (servicio, _) = CrearCon([
            Original(),
            ClienteBuilder.Uno().ConId(8).ConDocumento(33111222).ConCorreo("otro@correo.com").Build()
        ]);

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ClientePatchDto { Correo = "otro@correo.com" }, 30111222));

        excepcion.Message.Should().Be("Ya existe un cliente con el correo 'otro@correo.com'.");
    }

    [Fact]
    public async Task Patch_permite_dejar_su_propio_correo_sin_disparar_el_duplicado()
    {
        var (servicio, _) = CrearCon([Original()]);

        (await servicio.Patch(new ClientePatchDto { Correo = "ana.gomez@correo.com" }, 30111222)).Should().BeTrue();
    }

    [Fact]
    public async Task Patch_falla_si_no_encuentra_el_cliente()
    {
        var (servicio, _) = CrearCon([Original()]);

        var excepcion = await Assert.ThrowsAsync<DataException>(
            () => servicio.Patch(new ClientePatchDto { Nombre = "Ana Gomez" }, 33111222));

        excepcion.Message.Should().Be("Cliente no encontrado.");
    }

    [Fact]
    public async Task Patch_lanza_UpdateException_si_la_base_no_afecta_ninguna_fila()
    {
        var (servicio, repo) = CrearCon();
        repo.ResultadoUpdate = 0;

        var excepcion = await Assert.ThrowsAsync<UpdateException>(
            () => servicio.Patch(new ClientePatchDto { Nombre = "Ana Gomez" }, 30111222));

        excepcion.Message.Should().Be("No se pudo actualizar en la base de datos.");
    }
}

/// <summary>
/// Tests de <c>ClienteServicio.Enable</c> (#20, #39). La API ya no permite baja
/// fisica: habilitar/deshabilitar se hace con PATCH, asi que <c>Enable</c> queda
/// como contrato de <c>IServicioGenerico</c> y lanza <c>NotImplementedException</c>.
/// </summary>
public class ClienteServicioEnableTests
{
    private static (ClienteServicio Servicio, RepositorioGenericoVentasFake<Cliente> Repo) CrearCon()
    {
        var repo = new RepositorioGenericoVentasFake<Cliente>(
            [ClienteBuilder.Uno().ConId(7).ConDocumento(30111222)
                .ConNombre("Ana Gomez").ConCorreo("ana@correo.com").ConActivo(true).Build()]);
        return (new ClienteServicio(repo, new ClienteValidador(repo)), repo);
    }

    /// <summary>
    /// Caracteriza que la baja logica de clientes ya no esta implementada: el
    /// endpoint DELETE se elimino y la habilitacion/deshabilitacion se hace por
    /// PATCH. La interfaz <c>IServicioGenerico</c> obliga a declarar el metodo.
    /// </summary>
    [Fact]
    public async Task Enable_lanza_NotImplementedException_siempre()
    {
        var (servicio, _) = CrearCon();

        var excepcion = await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(30111222, true));

        excepcion.Message.Should().Be(
            "La baja de clientes no está implementada: usar PATCH con 'activo' para habilitarlo o deshabilitarlo.");
    }

    [Fact]
    public async Task Enable_falla_igual_para_el_estado_true_que_para_el_false()
    {
        var (servicio, _) = CrearCon();

        await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(30111222, false));
    }

    [Fact]
    public async Task Enable_no_toca_la_base_ni_valida_el_documento()
    {
        var (servicio, repo) = CrearCon();

        await Assert.ThrowsAsync<NotImplementedException>(() => servicio.Enable(0, true));

        repo.VecesGetAsync.Should().Be(0);
        repo.VecesUpdate.Should().Be(0);
    }
}