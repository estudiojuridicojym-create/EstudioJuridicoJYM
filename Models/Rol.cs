using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("roles")]
public class Rol
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(50)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Descripcion { get; set; }

    public bool Estado { get; set; } = true;

    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
}
