using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SystemManer.Data;
using SystemManer.Models;
using SystemManer.ViewModels;

namespace SystemManer.Controllers
{
    public class IncidenciasController : Controller
    {
        private const long LimiteTotal = 300L * 1024 * 1024; // 300 MB por envío
        private const long MaxImagen = 5L * 1024 * 1024;     // 5 MB por foto
        private const long MaxVideo = 100L * 1024 * 1024;    // 100 MB por video

        private static readonly string[] ExtImagen = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] ExtVideo = { ".mp4", ".webm", ".mov" };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<IdentityUser> _userManager;

        public IncidenciasController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        // ---------- HISTORIAL + BUSCADOR ----------

        [HttpGet]
        [Authorize(Policy = Permisos.Leer)]
        public async Task<IActionResult> Historial(string? q, EstadoIncidencia? estado)
        {
            var query = _context.Incidencias
                .AsNoTracking()
                .Include(i => i.Maquina)
                .Include(i => i.Usuario)
                .Include(i => i.Archivos)
                .AsQueryable();

            // El técnico solo ve lo suyo; supervisor y admin ven todo
            if (!User.EsRevisor())
            {
                var miId = User.IdUsuario();
                query = query.Where(i => i.UsuarioId == miId);
            }

            if (estado.HasValue)
                query = query.Where(i => i.Estado == estado.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(i =>
                    i.Problema.Contains(q) ||
                    (i.Solucion != null && i.Solucion.Contains(q)) ||
                    i.Maquina!.Nombre.Contains(q));
            }

            ViewData["Busqueda"] = q;
            ViewData["Estado"] = estado;

            var lista = await query.OrderByDescending(i => i.Fecha).ToListAsync();
            return View(lista);
        }

        // ---------- DETALLE ----------

        [HttpGet]
        [Authorize(Policy = Permisos.Leer)]
        public async Task<IActionResult> Detalle(int id)
        {
            var incidencia = await _context.Incidencias
                .AsNoTracking()
                .Include(i => i.Maquina)
                .Include(i => i.Usuario)
                .Include(i => i.Archivos)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (incidencia is null)
                return NotFound();

            if (!incidencia.PuedeVer(User))
                return Forbid();

            return View(incidencia);
        }

        // ---------- REGISTRAR ----------

        [HttpGet]
        [Authorize(Policy = Permisos.Crear)]
        public async Task<IActionResult> Registrar()
        {
            return await VistaFormularioAsync(new IncidenciaFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permisos.Crear)]
        [RequestSizeLimit(LimiteTotal)]
        [RequestFormLimits(MultipartBodyLengthLimit = LimiteTotal)]
        public async Task<IActionResult> Registrar(IncidenciaFormViewModel model, string accion = "guardar")
        {
            await ValidarAsync(model, accion);

            if (!ModelState.IsValid)
                return await VistaFormularioAsync(model);

            var incidencia = new Incidencia
            {
                MaquinaId = model.MaquinaId,
                Problema = model.Problema.Trim(),
                Solucion = LimpiarTexto(model.Solucion),
                UsuarioId = User.IdUsuario(),
                Fecha = DateTime.Now,
                Estado = EstadoIncidencia.Borrador
            };

            await AgregarArchivosAsync(incidencia, model.Archivos);

            _context.Incidencias.Add(incidencia);
            await _context.SaveChangesAsync();

            if (accion == "enviar")
            {
                await EnviarAsync(incidencia);
                TempData["Mensaje"] = "Enviado al supervisor para su validación.";
            }
            else
            {
                TempData["Mensaje"] = "Borrador guardado.";
            }

            return RedirectToAction(nameof(Detalle), new { id = incidencia.Id });
        }

        // ---------- EDITAR ----------

        [HttpGet]
        [Authorize(Policy = Permisos.Editar)]
        public async Task<IActionResult> Editar(int id)
        {
            var incidencia = await _context.Incidencias.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (incidencia is null)
                return NotFound();

            if (!incidencia.PuedeEditar(User))
                return Forbid();

            return await VistaFormularioAsync(new IncidenciaFormViewModel
            {
                Id = incidencia.Id,
                MaquinaId = incidencia.MaquinaId,
                Problema = incidencia.Problema,
                Solucion = incidencia.Solucion
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permisos.Editar)]
        [RequestSizeLimit(LimiteTotal)]
        [RequestFormLimits(MultipartBodyLengthLimit = LimiteTotal)]
        public async Task<IActionResult> Editar(IncidenciaFormViewModel model, string accion = "guardar")
        {
            var incidencia = await _context.Incidencias
                .Include(i => i.Archivos)
                .FirstOrDefaultAsync(i => i.Id == model.Id);

            if (incidencia is null)
                return NotFound();

            if (!incidencia.PuedeEditar(User))
                return Forbid();

            await ValidarAsync(model, accion);

            if (!ModelState.IsValid)
                return await VistaFormularioAsync(model);

            incidencia.MaquinaId = model.MaquinaId;
            incidencia.Problema = model.Problema.Trim();
            incidencia.Solucion = LimpiarTexto(model.Solucion);

            // Quitar archivos marcados
            var aQuitar = incidencia.Archivos.Where(a => model.EliminarArchivosIds.Contains(a.Id)).ToList();
            foreach (var archivo in aQuitar)
            {
                EliminarDeDisco(archivo.Ruta);
                _context.Archivos.Remove(archivo);
            }

            // Agregar los nuevos
            await AgregarArchivosAsync(incidencia, model.Archivos);

            await _context.SaveChangesAsync();

            if (accion == "enviar")
            {
                await EnviarAsync(incidencia);
                TempData["Mensaje"] = "Enviado al supervisor para su validación.";
            }
            else
            {
                TempData["Mensaje"] = "Cambios guardados.";
            }

            return RedirectToAction(nameof(Detalle), new { id = incidencia.Id });
        }

        // ---------- ENVIAR (desde el detalle) ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar(int id)
        {
            var incidencia = await _context.Incidencias.FindAsync(id);
            if (incidencia is null)
                return NotFound();

            if (!incidencia.PuedeEnviar(User))
                return Forbid();

            if (string.IsNullOrWhiteSpace(incidencia.Solucion))
            {
                TempData["Error"] = "Escribe la solución antes de enviar (botón Editar).";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            await EnviarAsync(incidencia);

            TempData["Mensaje"] = "Enviado al supervisor para su validación.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        // ---------- VALIDAR / RECHAZAR (supervisor y admin) ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{Roles.Admin},{Roles.Supervisor}")]
        public async Task<IActionResult> Revisar(int id, bool aprobar, string? comentario)
        {
            var incidencia = await _context.Incidencias
                .Include(i => i.Maquina)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (incidencia is null)
                return NotFound();

            if (incidencia.Estado != EstadoIncidencia.Enviada)
            {
                TempData["Error"] = "Este registro no está pendiente de validación.";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            if (!aprobar && string.IsNullOrWhiteSpace(comentario))
            {
                TempData["Error"] = "Escribe el motivo del rechazo.";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            incidencia.Estado = aprobar ? EstadoIncidencia.Validada : EstadoIncidencia.Rechazada;
            incidencia.FechaRevision = DateTime.Now;
            incidencia.RevisadoPor = User.Identity?.Name;
            incidencia.ComentarioRevision = LimpiarTexto(comentario);

            await _context.SaveChangesAsync();

            var autor = await _userManager.FindByIdAsync(incidencia.UsuarioId);
            if (autor is not null)
            {
                await NotificarAsync(
                    new[] { autor },
                    $"Tu trabajo en \"{incidencia.Maquina?.Nombre}\" fue {(aprobar ? "validado" : "rechazado")}.",
                    incidencia.Id);
            }

            TempData["Mensaje"] = aprobar ? "Registro validado." : "Registro rechazado.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        // ---------- ELIMINAR ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permisos.Borrar)]
        public async Task<IActionResult> Eliminar(int id)
        {
            var incidencia = await _context.Incidencias
                .Include(i => i.Archivos)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (incidencia is null)
                return NotFound();

            if (!incidencia.PuedeBorrar(User))
                return Forbid();

            foreach (var archivo in incidencia.Archivos)
                EliminarDeDisco(archivo.Ruta);

            _context.Notificaciones.RemoveRange(_context.Notificaciones.Where(n => n.IncidenciaId == id));
            _context.Incidencias.Remove(incidencia); // sus archivos se borran en cascada
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Registro eliminado.";
            return RedirectToAction(nameof(Historial));
        }

        // ---------- Auxiliares ----------

        private async Task<IActionResult> VistaFormularioAsync(IncidenciaFormViewModel model)
        {
            model.Maquinas = await _context.Maquinas
                .AsNoTracking()
                .OrderBy(m => m.Nombre)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Nombre })
                .ToListAsync();

            if (model.Id != 0)
            {
                model.ArchivosActuales = await _context.Archivos
                    .AsNoTracking()
                    .Where(a => a.IncidenciaId == model.Id)
                    .ToListAsync();
            }

            return View("Formulario", model);
        }

        private async Task ValidarAsync(IncidenciaFormViewModel model, string accion)
        {
            if (!await _context.Maquinas.AnyAsync(m => m.Id == model.MaquinaId))
                ModelState.AddModelError(nameof(model.MaquinaId), "Selecciona una máquina válida.");

            if (accion == "enviar" && string.IsNullOrWhiteSpace(model.Solucion))
                ModelState.AddModelError(nameof(model.Solucion), "Escribe la solución antes de enviar.");

            foreach (var archivo in model.Archivos.Where(a => a.Length > 0))
            {
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

                if (ExtImagen.Contains(extension))
                {
                    if (archivo.Length > MaxImagen)
                        ModelState.AddModelError(nameof(model.Archivos), $"La foto \"{archivo.FileName}\" supera los 5 MB.");
                }
                else if (ExtVideo.Contains(extension))
                {
                    if (archivo.Length > MaxVideo)
                        ModelState.AddModelError(nameof(model.Archivos), $"El video \"{archivo.FileName}\" supera los 100 MB.");
                }
                else
                {
                    ModelState.AddModelError(nameof(model.Archivos), $"\"{archivo.FileName}\": formato no permitido.");
                }
            }
        }

        private async Task AgregarArchivosAsync(Incidencia incidencia, IEnumerable<IFormFile> archivos)
        {
            var carpeta = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(carpeta);

            foreach (var archivo in archivos.Where(a => a.Length > 0))
            {
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                var nombre = $"{Guid.NewGuid():N}{extension}";

                await using (var stream = new FileStream(Path.Combine(carpeta, nombre), FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                var original = Path.GetFileName(archivo.FileName);

                incidencia.Archivos.Add(new Archivo
                {
                    Ruta = $"/uploads/{nombre}",
                    NombreOriginal = original.Length > 200 ? original[..200] : original,
                    EsVideo = ExtVideo.Contains(extension)
                });
            }
        }

        private void EliminarDeDisco(string ruta)
        {
            var archivo = Path.Combine(_env.WebRootPath, "uploads", Path.GetFileName(ruta));
            if (System.IO.File.Exists(archivo))
                System.IO.File.Delete(archivo);
        }

        // Pasa el registro a "Enviada" y avisa a supervisores y admins
        private async Task EnviarAsync(Incidencia incidencia)
        {
            incidencia.Estado = EstadoIncidencia.Enviada;
            incidencia.FechaEnvio = DateTime.Now;
            incidencia.FechaRevision = null;
            incidencia.RevisadoPor = null;
            incidencia.ComentarioRevision = null;
            await _context.SaveChangesAsync();

            var maquina = await _context.Maquinas
                .AsNoTracking()
                .Where(m => m.Id == incidencia.MaquinaId)
                .Select(m => m.Nombre)
                .FirstOrDefaultAsync();

            var supervisores = await _userManager.GetUsersInRoleAsync(Roles.Supervisor);
            var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);

            await NotificarAsync(
                supervisores.Concat(admins).DistinctBy(u => u.Id),
                $"{User.Identity?.Name} envió un trabajo de \"{maquina}\" para validar.",
                incidencia.Id);
        }

        // Solo notifica a quien tiene el permiso "Notificaciones" (el Admin siempre) y no a quien la genera
        private async Task NotificarAsync(IEnumerable<IdentityUser> destinatarios, string mensaje, int incidenciaId)
        {
            var miId = User.IdUsuario();

            foreach (var usuario in destinatarios.Where(u => u.Id != miId))
            {
                var esAdmin = await _userManager.IsInRoleAsync(usuario, Roles.Admin);
                var claims = await _userManager.GetClaimsAsync(usuario);
                var recibe = esAdmin ||
                             claims.Any(c => c.Type == Permisos.Tipo && c.Value == Permisos.Notificaciones);

                if (!recibe)
                    continue;

                _context.Notificaciones.Add(new Notificacion
                {
                    UsuarioId = usuario.Id,
                    Mensaje = mensaje,
                    IncidenciaId = incidenciaId,
                    Fecha = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        private static string? LimpiarTexto(string? texto) =>
            string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}