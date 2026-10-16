using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("registro_actividades")]
public class RegistroActividad
{
    [Key]
    public long Id { get; set; }

    [Column("usuario_id")]
    public long UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    [Required, MaxLength(100)]
    public string Accion { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Modulo { get; set; } = string.Empty;

    [Column("registro_id")]
    public long? RegistroId { get; set; }

    public string? Descripcion { get; set; }

    [Column("fecha_hora")]
    public DateTime FechaHora { get; set; }
}
