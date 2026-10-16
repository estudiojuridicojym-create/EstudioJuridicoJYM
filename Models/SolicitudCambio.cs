using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

public enum TipoAccionSolicitud
{
    EDITAR,
    DESACTIVAR
}

public enum EstadoSolicitud
{
    PENDIENTE,
    APROBADA,
    RECHAZADA
}

// Cuando un ASESOR_LEGAL edita o desactiva un registro, el cambio no se
// aplica de inmediato: queda guardado aqui como propuesta y solo un
// ABOGADO puede aprobarlo (recien ahi se escribe sobre el registro real)
// o rechazarlo. Los ABOGADOS editan directo, sin pasar por esta tabla.
[Table("solicitudes_cambio")]
public class SolicitudCambio
{
    [Key]
    public long Id { get; set; }

    // "Clientes" o "Procesos": mismo nombre de modulo que usa la auditoria.
    [Required, MaxLength(50)]
    public string Modulo { get; set; } = string.Empty;

    // Id del registro afectado (cliente_id, proceso_id, etc.).
    [Column("registro_id")]
    public long RegistroId { get; set; }

    [Column("tipo_accion")]
    public TipoAccionSolicitud TipoAccion { get; set; }

    // Snapshot en JSON de los valores propuestos por el asesor. Al aprobar
    // se deserializa y se vuelca sobre la entidad real.
    [Column("datos_json")]
    public string? DatosJson { get; set; }

    // Texto legible ("Nombres: Juan -> Juan Carlos") para que el abogado
    // vea que esta aprobando sin tener que leer el JSON.
    [Column("resumen_cambios")]
    public string? ResumenCambios { get; set; }

    [Column("descripcion"), MaxLength(300)]
    public string? Descripcion { get; set; }

    [Column("solicitante_id")]
    public long SolicitanteId { get; set; }
    public Usuario? Solicitante { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.PENDIENTE;

    [Column("revisor_id")]
    public long? RevisorId { get; set; }
    public Usuario? Revisor { get; set; }

    [Column("motivo_rechazo"), MaxLength(300)]
    public string? MotivoRechazo { get; set; }

    [Column("fecha_solicitud")]
    public DateTime FechaSolicitud { get; set; } = DateTime.Now;

    [Column("fecha_revision")]
    public DateTime? FechaRevision { get; set; }
}
