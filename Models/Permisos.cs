using Microsoft.AspNetCore.Mvc;

namespace SystemManer.Models
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Supervisor = "Supervisor";
        public const string Tecnico = "Tecnico";

        public static readonly string[] Todos = { Admin, Supervisor, Tecnico };
    }

    public static class Permisos
    {
        public const string Tipo = "permiso"; // tipo de claim guardado en el usuario

        public const string Crear = "Crear";
        public const string Editar = "Editar";
        public const string Leer = "Leer";
        public const string Borrar = "Borrar";
        public const string Notificaciones = "Notificaciones";

        public static readonly string[] Todos = { Crear, Editar, Leer, Borrar, Notificaciones };

        // Permisos con los que nace cada rol (el admin luego los ajusta por usuario)
        public static string[] PorDefecto(string rol) => rol switch
        {
            Roles.Admin => Todos,
            Roles.Supervisor => Todos,
            _ => new[] { Crear, Editar, Leer }
        };
    }
}