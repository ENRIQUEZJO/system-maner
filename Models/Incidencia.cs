using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SystemManer.Models
{
    public enum EstadoIncidencia
    {
        Borrador = 0,
        Enviada = 1,
        Validada = 2,
        Rechazada = 3
    }

    public class Incidencia
    {
        public int Id { get; set; }

        public int MaquinaId { get; set; }
        public Maquina? Maquina { get; set; }

        [Required, StringLength(2000)]
        public string Problema { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Solucion { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        public EstadoIncidencia Estado { get; set; } = EstadoIncidencia.Borrador;

        // Autor (técnico, supervisor o admin que lo registró)
        public string UsuarioId { get; set; } = string.Empty;
        public IdentityUser? Usuario { get; set; }

        public DateTime? FechaEnvio { get; set; }
        public DateTime? FechaRevision { get; set; }

        [StringLength(256)]
        public string? RevisadoPor { get; set; }

        [StringLength(1000)]
        public string? ComentarioRevision { get; set; }

        public ICollection<Archivo> Archivos { get; set; } = new List<Archivo>();
    }
}