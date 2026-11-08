using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SystemManer.Data;
using SystemManer.Models;

namespace SystemManer.Controllers
{
    [Authorize(Policy = Permisos.Notificaciones)]
    public class NotificacionesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var miId = User.IdUsuario();

            var lista = await _context.Notificaciones
                .AsNoTracking()
                .Where(n => n.UsuarioId == miId)
                .OrderByDescending(n => n.Fecha)
                .Take(100)
                .ToListAsync();

            return View(lista);
        }

        // Marca como leída y lleva al registro
        [HttpGet]
        public async Task<IActionResult> Abrir(int id)
        {
            var miId = User.IdUsuario();
            var notificacion = await _context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == miId);

            if (notificacion is null)
                return NotFound();

            notificacion.Leida = true;
            await _context.SaveChangesAsync();

            if (notificacion.IncidenciaId.HasValue &&
                await _context.Incidencias.AnyAsync(i => i.Id == notificacion.IncidenciaId.Value))
            {
                return RedirectToAction("Detalle", "Incidencias", new { id = notificacion.IncidenciaId.Value });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarTodas()
        {
            var miId = User.IdUsuario();

            await _context.Notificaciones
                .Where(n => n.UsuarioId == miId && !n.Leida)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true));

            return RedirectToAction(nameof(Index));
        }
    }
}