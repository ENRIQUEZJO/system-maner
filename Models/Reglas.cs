using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SystemManer.Models
{
    public static class Reglas
    {
        public static string IdUsuario(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // El Admin tiene todos los permisos; el resto, solo los que el admin le dio
        public static bool Tiene(this ClaimsPrincipal user, string permiso) =>
            user.IsInRole(Roles.Admin) || user.HasClaim(Permisos.Tipo, permiso);

        public static bool EsRevisor(this ClaimsPrincipal user) =>
            user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Supervisor);

        public static bool PuedeVer(this Incidencia i, ClaimsPrincipal user) =>
            user.EsRevisor() || i.UsuarioId == user.IdUsuario();

        public static bool PuedeEditar(this Incidencia i, ClaimsPrincipal user) =>
            user.Tiene(Permisos.Editar) &&
            (user.EsRevisor() ||
             (i.UsuarioId == user.IdUsuario() &&
              (i.Estado == EstadoIncidencia.Borrador || i.Estado == EstadoIncidencia.Rechazada)));

        public static bool PuedeBorrar(this Incidencia i, ClaimsPrincipal user) =>
            user.Tiene(Permisos.Borrar) &&
            (user.EsRevisor() ||
             (i.UsuarioId == user.IdUsuario() && i.Estado != EstadoIncidencia.Validada));

        public static bool PuedeEnviar(this Incidencia i, ClaimsPrincipal user) =>
            i.UsuarioId == user.IdUsuario() &&
            (i.Estado == EstadoIncidencia.Borrador || i.Estado == EstadoIncidencia.Rechazada);

        public static bool PuedeRevisar(this Incidencia i, ClaimsPrincipal user) =>
            user.EsRevisor() && i.Estado == EstadoIncidencia.Enviada;

        public static string ClaseBadge(this EstadoIncidencia estado) => estado switch
        {
            EstadoIncidencia.Borrador => "bg-secondary",
            EstadoIncidencia.Enviada => "bg-warning text-dark",
            EstadoIncidencia.Validada => "bg-success",
            EstadoIncidencia.Rechazada => "bg-danger",
            _ => "bg-secondary"
        };
    }
}