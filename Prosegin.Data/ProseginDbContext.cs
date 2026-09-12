using Microsoft.EntityFrameworkCore;
using Prosegin.Data.Entities;

namespace Prosegin.Data;

public class ProseginDbContext : DbContext
{
    public ProseginDbContext(DbContextOptions<ProseginDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Producto> Productos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de la tabla Clientes
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ruc).HasMaxLength(11).IsRequired();
            entity.Property(e => e.RazonSocial).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DireccionFiscal).HasMaxLength(250);
            entity.Property(e => e.Telefono).HasMaxLength(20);
            entity.Property(e => e.CorreoElectronico).HasMaxLength(100);
        });

        // Configuración de la tabla Productos (EPP)
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sku).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Categoria).HasMaxLength(100);
            entity.Property(e => e.UnidadMedida).HasMaxLength(20);
            entity.Property(e => e.CostoReferencial).HasPrecision(18, 2);
            entity.Property(e => e.RutaFichaTecnicaPdf).HasMaxLength(300);
            entity.Property(e => e.NombreArchivoPdf).HasMaxLength(150);
        });
    }
}
