using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SystemManer.Models;
using SystemManer.ViewModels;

namespace SystemManer.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class UsuariosController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public UsuariosController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        // ---------- LISTA ----------

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var usuarios = await _userManager.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
            var actual = User.IdUsuario();
            var lista = new List<UsuarioListaItem>();

            foreach (var u in usuarios)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var claims = await _userManager.GetClaimsAsync(u);
                var esAdmin = roles.Contains(Roles.Admin);

                lista.Add(new UsuarioListaItem
                {
                    Id = u.Id,
                    Email = u.Email ?? u.UserName ?? string.Empty,
                    Rol = roles.FirstOrDefault() ?? "-",
                    PermisosTexto = esAdmin
                        ? "Todos"
                        : string.Join(", ", claims.Where(c => c.Type == Permisos.Tipo).Select(c => c.Value)),
                    Activo = !(u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow),
                    EsActual = u.Id == actual
                });
            }

            return View(lista);
        }

        // ---------- CREAR ----------

        [HttpGet]
        public IActionResult Crear()
        {
            return VistaFormulario(new UsuarioFormViewModel
            {
                Rol = Roles.Tecnico,
                PermisosSeleccionados = Permisos.PorDefecto(Roles.Tecnico).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(UsuarioFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "La contraseña es obligatoria.");

            if (!Roles.Todos.Contains(model.Rol))
                ModelState.AddModelError(nameof(model.Rol), "Rol no válido.");

            if (!ModelState.IsValid)
                return VistaFormulario(model);

            var usuario = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(usuario, model.Password!);
            if (!result.Succeeded)
            {
                AgregarErrores(result);
                return VistaFormulario(model);
            }

            await AsignarRolYPermisosAsync(usuario, model.Rol, model.PermisosSeleccionados);

            TempData["Mensaje"] = $"Usuario {model.Email} creado.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- EDITAR ----------

        [HttpGet]
        public async Task<IActionResult> Editar(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario is null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(usuario);
            var claims = await _userManager.GetClaimsAsync(usuario);

            return VistaFormulario(new UsuarioFormViewModel
            {
                Id = usuario.Id,
                Email = usuario.Email ?? usuario.UserName ?? string.Empty,
                Rol = roles.FirstOrDefault() ?? Roles.Tecnico,
                PermisosSeleccionados = claims.Where(c => c.Type == Permisos.Tipo).Select(c => c.Value).ToList(),
                EsActual = usuario.Id == User.IdUsuario()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(UsuarioFormViewModel model)
        {
            var usuario = model.Id is null ? null : await _userManager.FindByIdAsync(model.Id);
            if (usuario is null)
                return NotFound();

            model.Email = usuario.Email ?? usuario.UserName ?? string.Empty;
            model.EsActual = usuario.Id == User.IdUsuario();

            if (!Roles.Todos.Contains(model.Rol))
                ModelState.AddModelError(nameof(model.Rol), "Rol no válido.");

            // Un admin no puede quitarse a sí mismo el rol de admin
            if (model.EsActual && model.Rol != Roles.Admin)
                ModelState.AddModelError(nameof(model.Rol), "No puedes quitarte tu propio rol de Admin.");

            if (!ModelState.IsValid)
                return VistaFormulario(model);

            // Contraseña: solo se cambia si escribieron una nueva
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
                var result = await _userManager.ResetPasswordAsync(usuario, token, model.Password);
                if (!result.Succeeded)
                {
                    AgregarErrores(result);
                    return VistaFormulario(model);
                }
            }

            await AsignarRolYPermisosAsync(usuario, model.Rol, model.PermisosSeleccionados);

            TempData["Mensaje"] = "Usuario actualizado.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- ACTIVAR / DESACTIVAR ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario is null)
                return NotFound();

            if (usuario.Id == User.IdUsuario())
            {
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction(nameof(Index));
            }

            var estaActivo = !(usuario.LockoutEnd.HasValue && usuario.LockoutEnd > DateTimeOffset.UtcNow);

            if (estaActivo)
            {
                await _userManager.SetLockoutEnabledAsync(usuario, true);
                await _userManager.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);
                await _userManager.UpdateSecurityStampAsync(usuario); // cierra su sesión abierta
                TempData["Mensaje"] = $"Usuario {usuario.Email} desactivado.";
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(usuario, null);
                TempData["Mensaje"] = $"Usuario {usuario.Email} activado.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ---------- Auxiliares ----------

        private IActionResult VistaFormulario(UsuarioFormViewModel model)
        {
            model.RolesDisponibles = Roles.Todos
                .Select(r => new SelectListItem { Value = r, Text = r, Selected = r == model.Rol })
                .ToList();

            return View("Formulario", model);
        }

        private async Task AsignarRolYPermisosAsync(IdentityUser usuario, string rol, List<string> permisos)
        {
            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            if (!(rolesActuales.Count == 1 && rolesActuales[0] == rol))
            {
                if (rolesActuales.Count > 0)
                    await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
                await _userManager.AddToRoleAsync(usuario, rol);
            }

            var finales = rol == Roles.Admin
                ? Permisos.Todos
                : permisos.Where(p => Permisos.Todos.Contains(p)).Distinct().ToArray();

            var actuales = (await _userManager.GetClaimsAsync(usuario))
                .Where(c => c.Type == Permisos.Tipo).ToList();

            if (actuales.Count > 0)
                await _userManager.RemoveClaimsAsync(usuario, actuales);

            if (finales.Length > 0)
                await _userManager.AddClaimsAsync(usuario, finales.Select(p => new Claim(Permisos.Tipo, p)));
        }

        private void AgregarErrores(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                var texto = error.Code switch
                {
                    "DuplicateUserName" or "DuplicateEmail" => "Ya existe un usuario con ese correo.",
                    "PasswordTooShort" => "La contraseña es demasiado corta (mínimo 6 caracteres).",
                    _ => error.Description
                };
                ModelState.AddModelError(string.Empty, texto);
            }
        }
    }
}