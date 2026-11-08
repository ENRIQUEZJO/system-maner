using System.ComponentModel.DataAnnotations;

namespace SystemManer.Models
{
    public class Archivo
    {
        public int Id { get; set; }

        public int IncidenciaId { get; set; }
        public Incidencia? Incidencia { get; set; }

        [Required, StringLength(300)]
        public string Ruta { get; set; } = string.Empty;

        [StringLength(200)]
        public string NombreOriginal { get; set; } = string.Empty;

        public bool EsVideo { get; set; }
    }
}