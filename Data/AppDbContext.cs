using DashboardJym.Models;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<UsuarioRol> UsuariosRoles => Set<UsuarioRol>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<TipoProceso> TiposProceso => Set<TipoProceso>();
    public DbSet<Proceso> Procesos => Set<Proceso>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Cuota> Cuotas => Set<Cuota>();
    public DbSet<AbonoCuota> AbonosCuota => Set<AbonoCuota>();
    public DbSet<ActividadAgenda> Agenda => Set<ActividadAgenda>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<RegistroActividad> RegistrosActividad => Set<RegistroActividad>();
    public DbSet<SolicitudCambio> SolicitudesCambio => Set<SolicitudCambio>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ActuacionProceso> ActuacionesProceso => Set<ActuacionProceso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // usuarios: dni y correo unicos (equivalente a @Column(unique = true))
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Dni)
            .IsUnique();

        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Correo)
            .IsUnique();

        // roles: nombre unico
        modelBuilder.Entity<Rol>()
            .HasIndex(r => r.Nombre)
            .IsUnique();

        // usuarios_roles: clave compuesta (equivalente a @EmbeddedId UsuarioRolId)
        modelBuilder.Entity<UsuarioRol>()
            .HasKey(ur => new { ur.UsuarioId, ur.RolId });

        modelBuilder.Entity<UsuarioRol>()
            .HasOne(ur => ur.Usuario)
            .WithMany(u => u.UsuarioRoles)
            .HasForeignKey(ur => ur.UsuarioId);

        modelBuilder.Entity<UsuarioRol>()
            .HasOne(ur => ur.Rol)
            .WithMany(r => r.UsuarioRoles)
            .HasForeignKey(ur => ur.RolId);

        // clientes: dni unico, FK al usuario que registro al cliente
        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Dni)
            .IsUnique();

        modelBuilder.Entity<Cliente>()
            .HasOne(c => c.UsuarioRegistro)
            .WithMany()
            .HasForeignKey(c => c.UsuarioRegistroId);

        // procesos: FKs y enums guardados como texto (NUEVO, EN_PROCESO, etc.),
        // igual que @Enumerated(EnumType.STRING) en el proyecto Java
        modelBuilder.Entity<Proceso>()
            .Property(p => p.Estado)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Proceso>()
            .Property(p => p.Prioridad)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Proceso>()
            .HasOne(p => p.Cliente)
            .WithMany()
            .HasForeignKey(p => p.ClienteId);

        modelBuilder.Entity<Proceso>()
            .HasOne(p => p.TipoProceso)
            .WithMany()
            .HasForeignKey(p => p.TipoProcesoId);

        modelBuilder.Entity<Proceso>()
            .HasOne(p => p.AbogadoResponsable)
            .WithMany()
            .HasForeignKey(p => p.AbogadoResponsableId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Proceso>()
            .HasOne(p => p.AsesorResponsable)
            .WithMany()
            .HasForeignKey(p => p.AsesorResponsableId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Proceso>()
            .HasOne(p => p.UsuarioRegistro)
            .WithMany()
            .HasForeignKey(p => p.UsuarioRegistroId)
            .OnDelete(DeleteBehavior.Restrict);

        // actuaciones_proceso: cronología del expediente
        modelBuilder.Entity<ActuacionProceso>()
            .HasOne(a => a.Proceso)
            .WithMany()
            .HasForeignKey(a => a.ProcesoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActuacionProceso>()
            .HasOne(a => a.UsuarioRegistro)
            .WithMany()
            .HasForeignKey(a => a.UsuarioRegistroId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActuacionProceso>().Property(a => a.Fecha).HasColumnType("date");
        modelBuilder.Entity<ActuacionProceso>().Property(a => a.FechaRegistro).HasColumnType("timestamp without time zone");

        // pagos / cuotas / abonos: FKs y enums como texto
        modelBuilder.Entity<Pago>().Property(p => p.FormaPago).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Pago>().Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Pago>().Property(p => p.TipoPago).HasConversion<string>().HasMaxLength(20);

        modelBuilder.Entity<Pago>()
            .HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pago>()
            .HasOne(p => p.Proceso).WithMany().HasForeignKey(p => p.ProcesoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Pago>()
            .HasOne(p => p.UsuarioRegistro).WithMany().HasForeignKey(p => p.UsuarioRegistroId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pago>()
            .HasOne(p => p.UsuarioConfirmacion).WithMany().HasForeignKey(p => p.UsuarioConfirmacionId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Cuota>().Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Cuota>().Property(c => c.FormaPago).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Cuota>()
            .HasOne(c => c.Pago).WithMany(p => p.Cuotas).HasForeignKey(c => c.PagoId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AbonoCuota>().Property(a => a.FormaPago).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<AbonoCuota>()
            .HasOne(a => a.Cuota).WithMany().HasForeignKey(a => a.CuotaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AbonoCuota>()
            .HasOne(a => a.UsuarioRegistro).WithMany().HasForeignKey(a => a.UsuarioRegistroId).OnDelete(DeleteBehavior.Restrict);

        // agenda: FKs y enums como texto
        modelBuilder.Entity<ActividadAgenda>().Property(a => a.Tipo).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<ActividadAgenda>().Property(a => a.Estado).HasConversion<string>().HasMaxLength(20);

        modelBuilder.Entity<SolicitudCambio>().Property(s => s.TipoAccion).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<SolicitudCambio>().Property(s => s.Estado).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<SolicitudCambio>()
            .HasOne(s => s.Solicitante).WithMany().HasForeignKey(s => s.SolicitanteId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SolicitudCambio>()
            .HasOne(s => s.Revisor).WithMany().HasForeignKey(s => s.RevisorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActividadAgenda>()
            .HasOne(a => a.Cliente).WithMany().HasForeignKey(a => a.ClienteId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ActividadAgenda>()
            .HasOne(a => a.Proceso).WithMany().HasForeignKey(a => a.ProcesoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ActividadAgenda>()
            .HasOne(a => a.Responsable).WithMany().HasForeignKey(a => a.ResponsableId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ActividadAgenda>()
            .HasOne(a => a.UsuarioRegistro).WithMany().HasForeignKey(a => a.UsuarioRegistroId).OnDelete(DeleteBehavior.Restrict);

        // mensajes
        modelBuilder.Entity<Mensaje>()
            .HasOne(m => m.Remitente).WithMany().HasForeignKey(m => m.RemitenteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Mensaje>()
            .HasOne(m => m.Destinatario).WithMany().HasForeignKey(m => m.DestinatarioId).OnDelete(DeleteBehavior.Restrict);

        // notificaciones
        modelBuilder.Entity<Notificacion>()
            .HasOne(n => n.Usuario).WithMany().HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // registro de actividades (auditoria)
        modelBuilder.Entity<RegistroActividad>()
            .HasOne(r => r.Usuario).WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // password_reset_tokens: token unico, FK al usuario dueño del token
        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.Token)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.Usuario)
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordResetToken>().Property(t => t.FechaCreacion).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<PasswordResetToken>().Property(t => t.FechaExpiracion).HasColumnType("timestamp without time zone");

        // --- Mapeo explicito de columnas de fecha/hora ---
        // Toda la base de datos usa "date" y "timestamp" (SIN zona horaria) en
        // sus columnas de fecha (ver los scripts etapaX_schema.sql), nunca
        // "timestamp with time zone". Sin esta configuracion, EF Core/Npgsql
        // asume por defecto "timestamp with time zone" para cualquier
        // propiedad DateTime, lo que exige que todos los valores tengan
        // Kind=Utc y lanza una excepcion con valores Kind=Local o Unspecified
        // (como los que produce DateTime.Now o DateTime.Today). Fijar aqui el
        // tipo real de columna evita esa validacion.
        modelBuilder.Entity<ActividadAgenda>().Property(a => a.Fecha).HasColumnType("date");

        modelBuilder.Entity<Cliente>().Property(c => c.FechaNacimiento).HasColumnType("date");
        modelBuilder.Entity<Cliente>().Property(c => c.FechaRegistro).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<Cliente>().Property(c => c.FechaActualizacion).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<Cuota>().Property(c => c.FechaVencimiento).HasColumnType("date");
        modelBuilder.Entity<Cuota>().Property(c => c.FechaPago).HasColumnType("date");

        modelBuilder.Entity<AbonoCuota>().Property(a => a.FechaAbono).HasColumnType("date");
        modelBuilder.Entity<AbonoCuota>().Property(a => a.FechaRegistro).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<Mensaje>().Property(m => m.FechaLectura).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<Mensaje>().Property(m => m.FechaEnvio).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<Notificacion>().Property(n => n.FechaLectura).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<Notificacion>().Property(n => n.FechaCreacion).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<Pago>().Property(p => p.FechaPago).HasColumnType("date");
        modelBuilder.Entity<Pago>().Property(p => p.FechaConfirmacion).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<Pago>().Property(p => p.FechaRegistro).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<Pago>().Property(p => p.FechaActualizacion).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<Proceso>().Property(p => p.FechaInicio).HasColumnType("date");
        modelBuilder.Entity<Proceso>().Property(p => p.FechaRegistro).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<Proceso>().Property(p => p.FechaActualizacion).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<RegistroActividad>().Property(r => r.FechaHora).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<SolicitudCambio>().Property(s => s.FechaSolicitud).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<SolicitudCambio>().Property(s => s.FechaRevision).HasColumnType("timestamp without time zone");

        modelBuilder.Entity<Usuario>().Property(u => u.FechaCreacion).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<Usuario>().Property(u => u.FechaActualizacion).HasColumnType("timestamp without time zone");
    }
}
