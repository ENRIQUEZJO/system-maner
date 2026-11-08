using System.ComponentModel.DataAnnotations;

namespace SystemManer.Models
{
    public class Maquina
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Ubicación")]
        public string? Ubicacion { get; set; }

        public ICollection<Incidencia> Incidencias { get; set; } = new List<Incidencia>();
    }
}