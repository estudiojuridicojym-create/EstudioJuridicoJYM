using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

// En EF Core la clave compuesta (usuario_id, rol_id) se declara en el DbContext
// con Fluent API (HasKey), por eso aqui no se necesita una clase aparte tipo
// UsuarioRolId.java: los dos IDs de esta clase ya cumplen ese rol.
[Table("usuarios_roles")]
public class UsuarioRol
{
    [Column("usuario_id")]
    public long UsuarioId { get; set; }

    [Column("rol_id")]
    public long RolId { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Rol Rol { get; set; } = null!;
}
