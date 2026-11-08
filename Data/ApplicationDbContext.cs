using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SystemManer.Models;

namespace SystemManer.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Maquina> Maquinas => Set<Maquina>();
        public DbSet<Incidencia> Incidencias => Set<Incidencia>();
        public DbSet<Archivo> Archivos => Set<Archivo>();
        public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Incidencia>(e =>
            {
                e.HasOne(i => i.Maquina)
                 .WithMany(m => m.Incidencias)
                 .HasForeignKey(i => i.MaquinaId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(i => i.Usuario)
                 .WithMany()
                 .HasForeignKey(i => i.UsuarioId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}