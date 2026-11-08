using System.ComponentModel.DataAnnotations;

namespace SystemManer.Models
{
    public class Notificacion
    {
        public int Id { get; set; }

        public string UsuarioId { get; set; } = string.Empty;

        [StringLength(300)]
        public string Mensaje { get; set; } = string.Empty;

        public int? IncidenciaId { get; set; }

        public bool Leida { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}