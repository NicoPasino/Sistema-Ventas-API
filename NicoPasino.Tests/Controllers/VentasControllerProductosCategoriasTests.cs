using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NicoPasino.Controllers;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;
using NicoPasino.Tests.Common;
using NicoPasino.Tests.Common.Builders;

namespace NicoPasino.Tests.Controllers;

/// <summary>
/// Tests de los 8 endpoints de <c>Ventas.Productos.cs</c> (productos + categorias) (#26).
/// <para>
/// Patron a verificar en todos los actions: <c>DataException -> 400</c> con
/// <c>{ message }</c>, excepcion generica -> <c>500</c> con el string
/// <c>"Error de servidor: StatusCode 500"</c>. Se aserta el status code y el shape
/// del body, no solo que no lance.
/// </para>
/// <para>
/// <b>Los 202 de POST y PUT</b>: <c>CrearProducto</c> devuelve
/// <c>StatusCode(202, "Producto Creado")</c> y <c>ActualizarProducto</c> un
/// <c>ObjectResult("Actualizado")</c> con 202. Y ojo con los mensajes de la rama
/// generica: POST y PUT terminan con punto (<c>"Error de servidor: StatusCode 500. "</c>),
/// el resto no.
/// </para>
/// <para>
/// <b>La trampa del PATCH</b>: <c>VentasController</c> recibe <c>ProductoServicio</c>
/// como clase concreta y <c>Patch</c> es no-virtual, asi que no se puede sustituir.
/// El harness lo arma real, con el repo fake y un <c>ProductoValidador</c> real: el
/// test cubre controller + validacion de verdad.
/// </para>
/// </summary>
public class VentasControllerProductosCategoriasTests
{
    private static ProductoDto DtoProducto(int? idPublica = 1001)
        => new() {
            IdPublica = idPublica,
            IdCategoria = 1,
            Nombre = "Gaseosa 500ml",
            Descripcion = "Botella de plástico",
            Cantidad = 50,
            Precio = 1500.50m,
            StockMinimo = 5,
            StockMaximo = 100,
            Proveedor = "Distribuidora Central",
            Activo = true
        };

    // ============================================================== GET Productos

    [Fact]
    public async Task GetAll_productos_devuelve_200_con_la_coleccion_del_servicio()
    {
        var h = new VentasControllerHarness();
        var esperado = new[] { DtoProducto() };
        h.ServicioProducto.GetAll(true).Returns(esperado);

        var resultado = await h.Controller.GetAllProductos();

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task GetAll_productos_sin_datos_devuelve_200_con_coleccion_vacia()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.GetAll(true).Returns([]);

        var resultado = await h.Controller.GetAllProductos();

        resultado.Estado().Should().Be(200);
        ((IEnumerable<ProductoDto>)resultado.Cuerpo()!).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_productos_cuando_el_servicio_falla_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.GetAll(true)
            .Returns(Task.FromException<IEnumerable<ProductoDto>>(new Exception("boom")));

        var resultado = await h.Controller.GetAllProductos();

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500");
    }

    // ========================================================= GET Productos/search

    [Fact]
    public async Task Search_productos_devuelve_200_con_los_resultados()
    {
        var h = new VentasControllerHarness();
        var esperado = new[] { DtoProducto() };
        h.ServicioProducto.GetAll(Arg.Any<string>(), Arg.Any<string?>()).Returns(esperado);

        var resultado = await h.Controller.GetAllProductos("nombre", "gaseosa");

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task Search_productos_con_campo_no_soportado_devuelve_400_con_el_mensaje()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.GetAll(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<IEnumerable<ProductoDto>>(
                new DataException("Campo de búsqueda 'x' no soportado.")));

        var resultado = await h.Controller.GetAllProductos("x", "v");

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Campo de búsqueda 'x' no soportado.");
    }

    /// <summary>
    /// Con el campo vacio el servicio lanza <c>DataException</c> -> 400 (#36).
    /// </summary>
    [Fact]
    public async Task Search_productos_con_campo_vacio_devuelve_400()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.GetAll(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<IEnumerable<ProductoDto>>(
                new DataException("Campo de búsqueda no válido.")));

        var resultado = await h.Controller.GetAllProductos("", null);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Campo de búsqueda no válido.");
    }

    // ============================================================== GET Productos/{id}

    [Fact]
    public async Task GetProducto_existente_devuelve_200_con_el_producto()
    {
        var h = new VentasControllerHarness();
        var producto = DtoProducto(1001);
        h.ServicioProducto.GetById(7).Returns(producto);

        var resultado = await h.Controller.GetProducto(7);

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(producto);
    }

    [Fact]
    public async Task GetProducto_con_IdPublica_null_devuelve_404_con_el_mensaje()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.GetById(7).Returns(new ProductoDto());

        var resultado = await h.Controller.GetProducto(7);

        resultado.Estado().Should().Be(404);
        resultado.Prop("message").Should().Be("Producto no encontrado");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetProducto_con_id_invalido_devuelve_400(int id)
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.GetById(Arg.Any<int>())
            .Returns(Task.FromException<ProductoDto>(new DataException("Id no válido")));

        var resultado = await h.Controller.GetProducto(id);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Id no válido");
    }

    [Fact]
    public async Task GetProducto_cuando_el_servicio_falla_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.GetById(1).Returns(Task.FromException<ProductoDto>(new Exception("boom")));

        var resultado = await h.Controller.GetProducto(1);

        resultado.Estado().Should().Be(500);
    }

    // ============================================================== POST Productos

    [Fact]
    public async Task Crear_producto_exitoso_devuelve_202_con_Producto_Creado()
    {
        var h = new VentasControllerHarness();
        var dto = DtoProducto();
        h.ServicioProducto.Create(Arg.Any<ProductoDto>()).Returns(true);

        var resultado = await h.Controller.CrearProducto(dto);

        resultado.Estado().Should().Be(202, "POST de productos devuelve 202 Accepted");
        resultado.Cuerpo().Should().Be("Producto Creado");
        _ = h.ServicioProducto.Received(1).Create(dto);
    }

    [Fact]
    public async Task Crear_producto_con_body_null_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.CrearProducto(null!);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Datos inválidos, por favor revisar.");
    }

    [Fact]
    public async Task Crear_producto_con_DataException_devuelve_400()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.Create(Arg.Any<ProductoDto>())
            .Returns(Task.FromException<bool>(new DataException("La categoría indicada no existe.")));

        var resultado = await h.Controller.CrearProducto(DtoProducto());

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("La categoría indicada no existe.");
    }

    [Fact]
    public async Task Crear_producto_cuando_Create_devuelve_false_devuelve_500_con_mensaje_con_punto()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.Create(Arg.Any<ProductoDto>()).Returns(false);

        var resultado = await h.Controller.CrearProducto(DtoProducto());

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500. ", "la rama generica de POST/PUT termina con punto");
    }

    // =============================================================== PUT Productos

    [Fact]
    public async Task Actualizar_producto_exitoso_devuelve_202_con_Actualizado()
    {
        var h = new VentasControllerHarness();
        var dto = DtoProducto();
        h.ServicioProducto.Update(Arg.Any<ProductoDto>()).Returns(true);

        var resultado = await h.Controller.ActualizarProducto(dto);

        resultado.Estado().Should().Be(202);
        resultado.Cuerpo().Should().Be("Actualizado");
    }

    [Fact]
    public async Task Actualizar_producto_con_body_null_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.ActualizarProducto(null!);

        resultado.Estado().Should().Be(400);
    }

    [Fact]
    public async Task Actualizar_producto_con_UpdateException_devuelve_500_con_mensaje_con_punto()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.Update(Arg.Any<ProductoDto>())
            .Returns(Task.FromException<bool>(new UpdateException("No pudo actualizar en la base de datos.")));

        var resultado = await h.Controller.ActualizarProducto(DtoProducto());

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500. ");
    }

    [Fact]
    public async Task Actualizar_producto_con_DataException_devuelve_400()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.Update(Arg.Any<ProductoDto>())
            .Returns(Task.FromException<bool>(new DataException("Objeto original no encontrado.")));

        var resultado = await h.Controller.ActualizarProducto(DtoProducto());

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Objeto original no encontrado.");
    }

    [Fact]
    public async Task Actualizar_producto_cuando_Update_devuelve_false_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioProducto.Update(Arg.Any<ProductoDto>()).Returns(false);

        var resultado = await h.Controller.ActualizarProducto(DtoProducto());

        resultado.Estado().Should().Be(500);
    }

    // ==================================================== PATCH Productos/{idPublica}
    // Aca se usa el ProductoServicio real (Patch es no-virtual).

    [Fact]
    public async Task Patch_producto_exitoso_actualiza_y_devuelve_202()
    {
        var h = new VentasControllerHarness(productos: [ProductoBuilder.Uno().ConId(1).ConIdPublica(5).Build()]);

        var resultado = await h.Controller.ActualizarProductoParcial(
            5, new ProductoPatchDto { Nombre = "Chocolate 100g" });

        resultado.Estado().Should().Be(202);
        resultado.Cuerpo().Should().Be("Actualizado");
        h.RepoProducto.Entidades.Single().Nombre.Should().Be("Chocolate 100g");
    }

    [Fact]
    public async Task Patch_producto_con_id_invalido_devuelve_400()
    {
        var h = new VentasControllerHarness(productos: [ProductoBuilder.Uno().ConId(1).ConIdPublica(5).Build()]);

        var resultado = await h.Controller.ActualizarProductoParcial(
            0, new ProductoPatchDto { Nombre = "Chocolate 100g" });

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("No se recibió ningún ID.");
    }

    [Fact]
    public async Task Patch_producto_inexistente_devuelve_400()
    {
        var h = new VentasControllerHarness();

        var resultado = await h.Controller.ActualizarProductoParcial(
            999, new ProductoPatchDto { Nombre = "Chocolate 100g" });

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Producto no encontrado.");
    }

    [Fact]
    public async Task Patch_producto_con_body_null_devuelve_400()
    {
        var h = new VentasControllerHarness(productos: [ProductoBuilder.Uno().ConId(1).ConIdPublica(5).Build()]);

        var resultado = await h.Controller.ActualizarProductoParcial(5, null!);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Datos inválidos, por favor revisar.", "el controller valida el body antes de llegar al servicio");
    }

    [Fact]
    public async Task Patch_producto_sin_datos_devuelve_400()
    {
        var h = new VentasControllerHarness(productos: [ProductoBuilder.Uno().ConId(1).ConIdPublica(5).Build()]);

        var resultado = await h.Controller.ActualizarProductoParcial(5, new ProductoPatchDto());

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("No se recibieron datos para actualizar.");
    }

    // No hay tests de DELETE Productos: el endpoint se elimino y DELETE
    // /Productos/{id} responde 405 (ver Endpoints405Tests).

    // ============================================================== GET Categorias

    [Fact]
    public async Task GetAll_categorias_devuelve_200_con_la_coleccion_del_servicio()
    {
        var h = new VentasControllerHarness();
        var esperado = new[] { new CategoriaDto { Id = 1, Nombre = "Bebidas" } };
        h.ServicioCategoria.GetAll(true).Returns(esperado);

        var resultado = await h.Controller.GetAllCategorias();

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task GetAll_categorias_sin_datos_devuelve_200_con_coleccion_vacia()
    {
        var h = new VentasControllerHarness();
        h.ServicioCategoria.GetAll(true).Returns([]);

        var resultado = await h.Controller.GetAllCategorias();

        resultado.Estado().Should().Be(200);
        ((IEnumerable<CategoriaDto>)resultado.Cuerpo()!).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_categorias_cuando_el_servicio_falla_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCategoria.GetAll(true)
            .Returns(Task.FromException<IEnumerable<CategoriaDto>>(new Exception("boom")));

        var resultado = await h.Controller.GetAllCategorias();

        resultado.Estado().Should().Be(500);
        resultado.Cuerpo().Should().Be("Error de servidor: StatusCode 500");
    }

    // ============================================================= GET Categorias/{id}

    [Fact]
    public async Task GetCategoria_existente_devuelve_200_con_la_categoria()
    {
        var h = new VentasControllerHarness();
        var categoria = new CategoriaDto { Id = 3, Nombre = "Bebidas" };
        h.ServicioCategoria.GetById(3).Returns(categoria);

        var resultado = await h.Controller.GetCategoria(3);

        resultado.Estado().Should().Be(200);
        resultado.Cuerpo().Should().BeSameAs(categoria);
    }

    [Fact]
    public async Task GetCategoria_con_Id_cero_devuelve_404_con_el_mensaje()
    {
        var h = new VentasControllerHarness();
        h.ServicioCategoria.GetById(999).Returns(new CategoriaDto());

        var resultado = await h.Controller.GetCategoria(999);

        resultado.Estado().Should().Be(404);
        resultado.Prop("message").Should().Be("Categoría no encontrada");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetCategoria_con_id_menor_a_uno_devuelve_400(int id)
    {
        var h = new VentasControllerHarness();
        h.ServicioCategoria.GetById(Arg.Any<int>())
            .Returns(Task.FromException<CategoriaDto>(new DataException("Id no válido.")));

        var resultado = await h.Controller.GetCategoria(id);

        resultado.Estado().Should().Be(400);
        resultado.Prop("message").Should().Be("Id no válido.");
    }

    [Fact]
    public async Task GetCategoria_cuando_el_servicio_falla_devuelve_500()
    {
        var h = new VentasControllerHarness();
        h.ServicioCategoria.GetById(1).Returns(Task.FromException<CategoriaDto>(new Exception("boom")));

        var resultado = await h.Controller.GetCategoria(1);

        resultado.Estado().Should().Be(500);
    }
}