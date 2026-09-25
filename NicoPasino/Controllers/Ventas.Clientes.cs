using Microsoft.AspNetCore.Mvc;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Errores;

namespace NicoPasino.Controllers
{
    public partial class VentasController
    {
        [HttpGet("Clientes")]
        public async Task<ActionResult> GetAllClientes() {
            try {
                var objs = await _clienteServicio.GetAll(true);
                return Ok(objs);
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpGet("Clientes/search/{campo}/{valor?}")]
        public async Task<ActionResult> GetAllClientes(string campo, string? valor) {
            try {
                var objs = await _clienteServicio.GetAll(campo, valor);
                return Ok(objs);
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpGet("Clientes/{id}")]
        public async Task<ActionResult> GetCliente(int id) {
            try {
                var obj = await _clienteServicio.GetById(id);
                if (obj != null) return Ok(obj);
                else return NotFound(new { message = "Cliente no encontrado" }); // 404
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpPost("Clientes")]
        public async Task<IActionResult> CrearCliente([FromBody] ClienteDto objeto) {
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _clienteServicio.Create(objeto);
                if (ok) return Ok(new { success = true });
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpPut("Clientes")]
        public async Task<IActionResult> ActualizarCliente([FromBody] ClienteDto objeto) {
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _clienteServicio.Update(objeto);
                if (ok) return Ok(new { success = true });
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500. ") { StatusCode = 500 };
            }
        }

        [HttpPatch("Clientes/{id}")]
        public async Task<IActionResult> ActualizarClienteParcial(int id, [FromBody] ClientePatchDto objeto) {
            //await Task.Delay(3000);
            try {
                if (objeto == null) throw new DataException("Datos inválidos, por favor revisar.");

                var ok = await _clienteServicioConcreto.Patch(objeto, id);
                if (ok) return Ok(new { success = true });
                else throw new Exception();
            }
            catch (DataException ex) {
                return BadRequest(new { message = ex.Message }); // 400
            }
            catch (Exception ex) {
                return new ObjectResult("Error de servidor: StatusCode 500") { StatusCode = 500 };
            }
        }

        [HttpDelete("Clientes/{id}")]
        public async Task<IActionResult> EliminarCliente(int id) {
            try {
                var res = await _clienteServicio.Enable(id, false);
                return Ok(new { success = true });
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
