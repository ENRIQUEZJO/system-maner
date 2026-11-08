using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemManer.Data;
using SystemManer.Models;

namespace SystemManer.Controllers
{
    [Authorize(Policy = Permisos.Crear)]
    public class MaquinasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MaquinasController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Registrar()
        {
            return View(new Maquina());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar([Bind("Nombre,Ubicacion")] Maquina maquina)
        {
            if (!ModelState.IsValid)
                return View(maquina);

            maquina.Nombre = maquina.Nombre.Trim();
            maquina.Ubicacion = maquina.Ubicacion?.Trim();

            _context.Maquinas.Add(maquina);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Máquina \"{maquina.Nombre}\" registrada.";
            return RedirectToAction(nameof(Registrar));
        }
    }
}