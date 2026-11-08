using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManer.Models;

namespace SystemManer.ViewModels
{
    public class IncidenciaFormViewModel
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Selecciona una máquina.")]
        [Display(Name = "Máquina")]
        public int MaquinaId { get; set; }

        [Required(ErrorMessage = "Describe el problema.")]
        [StringLength(2000)]
        public string Problema { get; set; } = string.Empty;

        [StringLength(2000)]
        [Display(Name = "Solución")]
        public string? Solucion { get; set; }

        // Fotos y videos nuevos
        [ValidateNever]
        public List<IFormFile> Archivos { get; set; } = new();

        // Ids de archivos existentes que se quieren quitar (solo al editar)
        [ValidateNever]
        public List<int> EliminarArchivosIds { get; set; } = new();

        [ValidateNever]
        public List<Archivo> ArchivosActuales { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> Maquinas { get; set; } = new();
    }
}