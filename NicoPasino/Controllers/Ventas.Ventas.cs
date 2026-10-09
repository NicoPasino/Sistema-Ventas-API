using Microsoft.AspNetCore.Mvc;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;

namespace NicoPasino.Controllers
{
    public partial class VentasController
    {
        [HttpGet("Ventas")]
        public async Task<ActionResult> GetAllVentas() {
            try {
                var objs = await _ventaServicio.GetAll(true);
                return Ok(objs);
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpGet("Ventas/search/{campo}/{valor?}")]
        public async Task<ActionResult> GetAllVentas(string campo, string? valor) {
            try {
                var objs = await _ventaServicio.GetAll(campo, valor);
                return Ok(objs);
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (ArgumentException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpGet("Ventas/{id}")]
        public async Task<ActionResult> GetVenta(int id) {
            try {
                var obj = await _ventaServicio.GetById(id);
                if (obj != null) return Ok(obj);
                else return NotFound(new { message = "Venta no encontrada" }); // 404
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpPost("Ventas")]
        public async Task<IActionResult> CrearVenta([FromBody] VentaDto objeto) {
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _ventaServicio.Create(objeto);
                if (ok) return Ok(new { ok = true, success = true });
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message });
            }
            catch (UpdateException ex) {
                return StatusCode(500, $"Error interno (500): {ex.Message}");
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpDelete("Ventas/{id}")]
        public async Task<IActionResult> EliminarVenta(int id) {
            try {
                var res = await _ventaServicio.Enable(id, false);
                return Ok(new { success = true });
            }
            catch (NotImplementedException ex) {
                return StatusCode(StatusCodes.Status501NotImplemented, new { message = ex.Message }); // 501
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }


        /* UPDATE
        [HttpPut("Ventas")]
        public async Task<IActionResult> ActualizarVenta([FromBody] ProductoDto objeto) {
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _ventaServicio.Update(objeto);
                if (ok) return Ok(new { success = true, message = "Data received successfully." });
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { error = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500. "*//* + ex.Message*//*) { StatusCode = 500 };
            }
        }*/
    }
}

// TODO: 
// refactorizar y reutilizar métodos de controladores