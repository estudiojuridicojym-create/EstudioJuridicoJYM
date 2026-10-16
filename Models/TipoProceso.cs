using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("tipos_proceso")]
public class TipoProceso
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Descripcion { get; set; }

    public bool Estado { get; set; } = true;
}
