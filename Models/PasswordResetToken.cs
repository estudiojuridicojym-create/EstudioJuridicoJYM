using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

// Token de un solo uso que se envia por correo cuando un usuario pide
// recuperar su contraseña. No reemplaza ni borra al usuario: solo permite
// definir una nueva contraseña si el token es valido, no ha expirado y no
// fue usado antes.
[Table("password_reset_tokens")]
public class PasswordResetToken
{
    [Key]
    public long Id { get; set; }

    [Column("usuario_id")]
    public long UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    // Cadena aleatoria (URL-safe) que se manda por correo dentro del enlace.
    [Required, MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    [Column("fecha_creacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    [Column("fecha_expiracion")]
    public DateTime FechaExpiracion { get; set; }

    [Column("usado")]
    public bool Usado { get; set; } = false;
}
