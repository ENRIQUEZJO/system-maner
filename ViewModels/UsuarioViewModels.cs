using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SystemManer.ViewModels
{
    public class UsuarioListaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string PermisosTexto { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public bool EsActual { get; set; }
    }

    // Sirve para crear y para editar (Id == null significa "nuevo")
    public class UsuarioFormViewModel
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [Display(Name = "Correo")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona un rol.")]
        public string Rol { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string? Password { get; set; }

        public List<string> PermisosSeleccionados { get; set; } = new();

        public bool EsActual { get; set; }

        [ValidateNever]
        public List<SelectListItem> RolesDisponibles { get; set; } = new();
    }
}