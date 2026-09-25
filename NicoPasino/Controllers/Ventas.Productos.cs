using Microsoft.AspNetCore.Mvc;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;

namespace NicoPasino.Controllers
{
    public partial class VentasController
    {
        [HttpGet("Productos")]
        public async Task<ActionResult> GetAllProductos() {
            try {
                var objs = await _productoServicio.GetAll(true);
                return Ok(objs);
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpGet("Productos/search/{campo}/{valor?}")]
        public async Task<ActionResult> GetAllProductos(string campo, string? valor) {
            try {
                var objs = await _productoServicio.GetAll(campo, valor);
                return Ok(objs);
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpGet("Productos/{id}")]
        public async Task<ActionResult> GetProducto(int id) {
            try {
                var obj = await _productoServicio.GetById(id);
                if (obj?.IdPublica != null) return Ok(obj);
                else return NotFound(new { message = "Producto no encontrado" }); // 404
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpPost("Productos")]
        public async Task<IActionResult> CrearProducto([FromBody] ProductoDto objeto) {
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _productoServicio.Create(objeto);
                if (ok) return StatusCode(202, "Producto Creado");
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500. " /*+ ex.Message*/) { StatusCode = 500 };
            }
        }

        [HttpPut("Productos")]
        public async Task<IActionResult> ActualizarProducto([FromBody] ProductoDto objeto) {
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _productoServicio.Update(objeto);
                if (ok) return new ObjectResult("Actualizado") { StatusCode = 202 };
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500. " /*+ ex.Message*/) { StatusCode = 500 };
            }
        }

        [HttpPatch("Productos/{idPublica}")]
        public async Task<IActionResult> ActualizarProductoParcial(int idPublica, [FromBody] ProductoPatchDto objeto) {
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _productoServicioConcreto.Patch(objeto, idPublica);
                if (ok) return StatusCode(202, "Actualizado");
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpDelete("Productos/{id}")]
        public async Task<IActionResult> EliminarProducto(int id) {
            try {
                var res = await _productoServicio.Enable(id, false);
                if (res) return Ok();
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        // -------------------------------------------
        // ------------ Categorías -------------------
        // -------------------------------------------

        [HttpGet("Categorias")]
        public async Task<ActionResult> GetAllCategorias() {
            try {
                var objs = await _categoriaServicio.GetAll(true);
                return Ok(objs);
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpGet("Categorias/{id}")]
        public async Task<ActionResult> GetCategoria(int id) {
            try {
                var obj = await _productoServicio.GetById(id);
                if (obj?.IdPublica != null) return Ok(obj);
                else return NotFound(new { message = "Categoría no encontrada" }); // 404
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }
    }
}
