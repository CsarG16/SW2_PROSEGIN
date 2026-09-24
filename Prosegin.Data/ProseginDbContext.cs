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
    public DbSet<PuntoEntrega> PuntosEntrega { get; set; }
    public DbSet<ContactoCliente> ContactosCliente { get; set; }
    public DbSet<Cotizacion> Cotizaciones { get; set; }
    public DbSet<CotizacionDetalle> CotizacionDetalles { get; set; }
    public DbSet<OrdenVenta> OrdenesVenta { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<OrdenCompra> OrdenesCompra { get; set; }
    public DbSet<OrdenCompraDetalle> OrdenCompraDetalles { get; set; }
    public DbSet<Factura> Facturas { get; set; }

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
            entity.Property(e => e.LimiteCredito).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.RepresentanteLegal).HasMaxLength(200);
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
            entity.Property(e => e.StockDisponible).HasDefaultValue(0);
            entity.Property(e => e.RutaFichaTecnicaPdf).HasMaxLength(300);
            entity.Property(e => e.NombreArchivoPdf).HasMaxLength(150);
        });

        modelBuilder.Entity<Cotizacion>(entity =>
        {
            entity.ToTable("Cotizaciones");
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.Igv).HasPrecision(18, 2);
            entity.Property(e => e.Total).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CotizacionDetalle>(entity =>
        {
            entity.ToTable("CotizacionDetalles");
            entity.Property(e => e.CostoProveedorReferencial).HasPrecision(18, 2);
            entity.Property(e => e.MargenDeseado).HasPrecision(18, 2);
            entity.Property(e => e.PrecioVentaCalculado).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrdenCompra>(entity =>
        {
            entity.ToTable("OrdenesCompra");
            entity.Property(e => e.Total).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrdenCompraDetalle>(entity =>
        {
            entity.ToTable("OrdenCompraDetalles");
            entity.Property(e => e.CostoUnitario).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Factura>(entity =>
        {
            entity.ToTable("Facturas");
            entity.Property(e => e.Total).HasPrecision(18, 2);
        });
        
        // Relación 1 a 1 de Cotizacion y OrdenVenta
        modelBuilder.Entity<Cotizacion>()
            .HasOne(c => c.OrdenVenta)
            .WithOne(o => o.Cotizacion)
            .HasForeignKey<OrdenVenta>(o => o.CotizacionId);
    }
}
