using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

// Cronología real del expediente: cada escrito presentado, resolución
// recibida o diligencia realizada queda como una fila aparte, con quién la
// registró y cuándo. Antes de esto, Proceso.UltimoActuado era un solo campo
// de texto que se sobrescribía cada vez, así que se perdía todo el
// historial anterior.
//
// IMPORTANTE: esta tabla es nueva, no viene en los scripts de base de
// datos existentes. Hace falta correr el SQL de migración (ver el mensaje
// donde se entrega este cambio) antes de desplegar, o la aplicación
// fallará con "la relación actuaciones_proceso no existe".
[Table("actuaciones_proceso")]
public class ActuacionProceso
{
    [Key]
    public long Id { get; set; }

    [Column("proceso_id")]
    public long ProcesoId { get; set; }
    public Proceso? Proceso { get; set; }

    [DataType(DataType.Date)]
    public DateTime Fecha { get; set; }

    [Required]
    public string Descripcion { get; set; } = string.Empty;

    [Column("usuario_registro_id")]
    public long UsuarioRegistroId { get; set; }
    public Usuario? UsuarioRegistro { get; set; }

    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; }
}
