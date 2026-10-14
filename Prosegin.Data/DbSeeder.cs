using Microsoft.EntityFrameworkCore;
using Prosegin.Data.Entities;

namespace Prosegin.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ProseginDbContext context)
    {
        // 1. Semillas de Proveedores
        var proveedores = new List<Proveedor>
        {
            new Proveedor
            {
                Ruc = "20100138056",
                RazonSocial = "3M PERÚ S.A.",
                UbicacionMalvinas = "Av. Canaval y Moreyra 641, San Isidro",
                Telefono = "(01) 224-5400",
                Contacto = "División de Seguridad Industrial"
            },
            new Proveedor
            {
                Ruc = "20512345678",
                RazonSocial = "IMPORTACIONES MALVINAS S.A.C.",
                UbicacionMalvinas = "C.C. Mesa Redonda Stand 104, Las Malvinas - Lima",
                Telefono = "987654321",
                Contacto = "Roberto Gómez"
            },
            new Proveedor
            {
                Ruc = "20509876543",
                RazonSocial = "DISTRIBUIDORA INDUSTRIAL DEL PERÚ S.A.",
                UbicacionMalvinas = "Av. Argentina 3088, Cercado de Lima",
                Telefono = "(01) 456-7890",
                Contacto = "Carlos Morales"
            },
            new Proveedor
            {
                Ruc = "20100223344",
                RazonSocial = "BATA PERÚ S.A.",
                UbicacionMalvinas = "Av. Industrial 550, Callao",
                Telefono = "(01) 513-2000",
                Contacto = "Línea Calzado Industrial"
            },
            new Proveedor
            {
                Ruc = "20601122334",
                RazonSocial = "SEGURIDAD INDUSTRIAL TOTAL S.A.C.",
                UbicacionMalvinas = "Jr. Huarochirí 230, Breña",
                Telefono = "976543210",
                Contacto = "Área de Cotizaciones"
            }
        };

        var existingProvRucs = new HashSet<string>(
            await context.Proveedores.Select(p => p.Ruc).ToListAsync());

        foreach (var prov in proveedores)
        {
            if (!existingProvRucs.Contains(prov.Ruc))
            {
                context.Proveedores.Add(prov);
            }
        }

        // 2. Semillas de Productos EPP
        var productos = new List<Producto>
        {
            new Producto
            {
                Sku = "CAS-3M-H700",
                Nombre = "CASCO DE SEGURIDAD INDUSTRIAL 3M H-700 CON SUSPENSIÓN RATCHET SECUREFIT",
                Descripcion = "Proveedor: 3M PERÚ S.A. / IMPORTACIONES MALVINAS",
                Categoria = "Protección de cabeza",
                UnidadMedida = "UND",
                CostoReferencial = 38.50m,
                StockDisponible = 85,
                RutaFichaTecnicaPdf = "/fichas/CAS-3M-H700.pdf",
                NombreArchivoPdf = "CAS-3M-H700.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "LEN-3M-VIRTUA",
                Nombre = "LENTES DE SEGURIDAD 3M VIRTUA PLUS ANTIRRAYADURA Y ANTIEMPAÑANTE",
                Descripcion = "Proveedor: 3M PERÚ S.A.",
                Categoria = "Protección ocular",
                UnidadMedida = "UND",
                CostoReferencial = 12.80m,
                StockDisponible = 150,
                RutaFichaTecnicaPdf = "/fichas/LEN-3M-VIRTUA.pdf",
                NombreArchivoPdf = "LEN-3M-VIRTUA.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "RES-3M-6200",
                Nombre = "RESPIRADOR DE MEDIA PIEZA FACIAL SERIE 6000 REUTILIZABLE 3M 6200",
                Descripcion = "Proveedor: 3M PERÚ S.A. / IMPORTACIONES MALVINAS",
                Categoria = "Protección respiratoria",
                UnidadMedida = "UND",
                CostoReferencial = 54.00m,
                StockDisponible = 40,
                RutaFichaTecnicaPdf = "/fichas/RES-3M-6200.pdf",
                NombreArchivoPdf = "RES-3M-6200.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "TAP-3M-1100",
                Nombre = "TAPONES AUDITIVOS DE ESPUMA SIN CORDÓN 3M 1100 NRR 29DB",
                Descripcion = "Proveedor: IMPORTACIONES MALVINAS S.A.C.",
                Categoria = "Protección auditiva",
                UnidadMedida = "PAR",
                CostoReferencial = 1.20m,
                StockDisponible = 600,
                RutaFichaTecnicaPdf = "/fichas/TAP-3M-1100.pdf",
                NombreArchivoPdf = "TAP-3M-1100.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "GUA-SHOWA-370",
                Nombre = "GUANTES DE NITRILO MULTIPROPÓSITO SHOWA 370 RESISTENTES A LA ABRASIÓN",
                Descripcion = "Proveedor: DISTRIBUIDORA INDUSTRIAL DEL PERÚ S.A.",
                Categoria = "Protección de manos",
                UnidadMedida = "PAR",
                CostoReferencial = 15.50m,
                StockDisponible = 120,
                RutaFichaTecnicaPdf = "/fichas/GUA-SHOWA-370.pdf",
                NombreArchivoPdf = "GUA-SHOWA-370.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "BOT-BATA-TITAN",
                Nombre = "BOTAS DE SEGURIDAD INDUSTRIAL BATA INDUSTRIALS TITAN CON PUNTERA DE ACERO",
                Descripcion = "Proveedor: BATA PERÚ S.A.",
                Categoria = "Protección de pies",
                UnidadMedida = "PAR",
                CostoReferencial = 145.00m,
                StockDisponible = 35,
                RutaFichaTecnicaPdf = "/fichas/BOT-BATA-TITAN.pdf",
                NombreArchivoPdf = "BOT-BATA-TITAN.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "ARN-HAWK-4A",
                Nombre = "ARNÉS DE SEGURIDAD CUERPO COMPLETO 4 ARGOLLAS HAWK MULTIPROPÓSITO ANSI",
                Descripcion = "Proveedor: SEGURIDAD INDUSTRIAL TOTAL S.A.C.",
                Categoria = "Trabajo en altura",
                UnidadMedida = "UND",
                CostoReferencial = 185.00m,
                StockDisponible = 20,
                RutaFichaTecnicaPdf = "/fichas/ARN-HAWK-4A.pdf",
                NombreArchivoPdf = "ARN-HAWK-4A.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "CHA-PRO-REF",
                Nombre = "CHALECO DE SEGURIDAD TIPO GEÓLOGO DRIL CON CINTAS REFLECTIVAS 3M",
                Descripcion = "Proveedor: IMPORTACIONES MALVINAS S.A.C.",
                Categoria = "Protección corporal",
                UnidadMedida = "UND",
                CostoReferencial = 28.00m,
                StockDisponible = 90,
                RutaFichaTecnicaPdf = "/fichas/CHA-PRO-REF.pdf",
                NombreArchivoPdf = "CHA-PRO-REF.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "CAS-MSA-VGUARD",
                Nombre = "CASCO DE SEGURIDAD INDUSTRIAL MSA V-GARD TIPO I CLASE E CON SUSPENSIÓN FAS-TRAC III",
                Descripcion = "Proveedor: DISTRIBUIDORA INDUSTRIAL DEL PERÚ S.A.",
                Categoria = "Protección de cabeza",
                UnidadMedida = "UND",
                CostoReferencial = 42.00m,
                StockDisponible = 70,
                RutaFichaTecnicaPdf = "/fichas/CAS-MSA-VGUARD.pdf",
                NombreArchivoPdf = "CAS-MSA-VGUARD.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "BAR-3M-4P",
                Nombre = "BARBIQUEJO DE 4 PUNTAS ELASTIZADO 3M PARA CASCO SERIE H-700",
                Descripcion = "Proveedor: 3M PERÚ S.A.",
                Categoria = "Protección de cabeza",
                UnidadMedida = "UND",
                CostoReferencial = 9.50m,
                StockDisponible = 140,
                RutaFichaTecnicaPdf = "/fichas/BAR-3M-4P.pdf",
                NombreArchivoPdf = "BAR-3M-4P.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "LEN-3M-SOLUS",
                Nombre = "LENTES DE SEGURIDAD 3M SOLUS SERIE 1000 CON TRATAMIENTO SCOTCHGARD ANTIEMPAÑANTE",
                Descripcion = "Proveedor: 3M PERÚ S.A.",
                Categoria = "Protección ocular",
                UnidadMedida = "UND",
                CostoReferencial = 32.00m,
                StockDisponible = 95,
                RutaFichaTecnicaPdf = "/fichas/LEN-3M-SOLUS.pdf",
                NombreArchivoPdf = "LEN-3M-SOLUS.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "VIS-STEEL-POLY",
                Nombre = "CARETA FACIAL DE POLICARBONATO CON CABEZAL AJUSTABLE STEELPRO CLEAR",
                Descripcion = "Proveedor: IMPORTACIONES MALVINAS S.A.C.",
                Categoria = "Protección ocular",
                UnidadMedida = "UND",
                CostoReferencial = 26.50m,
                StockDisponible = 60,
                RutaFichaTecnicaPdf = "/fichas/VIS-STEEL-POLY.pdf",
                NombreArchivoPdf = "VIS-STEEL-POLY.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "MAS-3M-8210",
                Nombre = "MASCARILLA RESPIRADORA DESCARTABLE N95 3M 8210 PARA POLVOS Y PARTÍCULAS",
                Descripcion = "Proveedor: 3M PERÚ S.A.",
                Categoria = "Protección respiratoria",
                UnidadMedida = "CJA",
                CostoReferencial = 68.00m,
                StockDisponible = 110,
                RutaFichaTecnicaPdf = "/fichas/MAS-3M-8210.pdf",
                NombreArchivoPdf = "MAS-3M-8210.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "FIL-3M-2097",
                Nombre = "FILTRO PARA PARTICULAS P100 CON ALIVIO CONTRA NIVELES MOLESTOS 3M 2097",
                Descripcion = "Proveedor: 3M PERÚ S.A. / IMPORTACIONES MALVINAS",
                Categoria = "Protección respiratoria",
                UnidadMedida = "PAR",
                CostoReferencial = 28.50m,
                StockDisponible = 180,
                RutaFichaTecnicaPdf = "/fichas/FIL-3M-2097.pdf",
                NombreArchivoPdf = "FIL-3M-2097.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "CAR-3M-6003",
                Nombre = "CARTUCHO MIXTO VAPORES ORGÁNICOS Y GASES ÁCIDOS 3M 6003",
                Descripcion = "Proveedor: 3M PERÚ S.A.",
                Categoria = "Protección respiratoria",
                UnidadMedida = "PAR",
                CostoReferencial = 49.00m,
                StockDisponible = 75,
                RutaFichaTecnicaPdf = "/fichas/CAR-3M-6003.pdf",
                NombreArchivoPdf = "CAR-3M-6003.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "ORE-3M-PELTOR-X4",
                Nombre = "OREJERA DE SEGURIDAD TIPO VINCHA 3M PELTOR SERIE X4A NRR 27DB",
                Descripcion = "Proveedor: 3M PERÚ S.A.",
                Categoria = "Protección auditiva",
                UnidadMedida = "UND",
                CostoReferencial = 98.00m,
                StockDisponible = 40,
                RutaFichaTecnicaPdf = "/fichas/ORE-3M-PELTOR-X4.pdf",
                NombreArchivoPdf = "ORE-3M-PELTOR-X4.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "ORE-3M-CASCO-H9",
                Nombre = "OREJERA ADAPTABLE A CASCO 3M OPTIME 98 H9P3E NRR 23DB",
                Descripcion = "Proveedor: 3M PERÚ S.A. / IMPORTACIONES MALVINAS",
                Categoria = "Protección auditiva",
                UnidadMedida = "PAR",
                CostoReferencial = 62.00m,
                StockDisponible = 55,
                RutaFichaTecnicaPdf = "/fichas/ORE-3M-CASCO-H9.pdf",
                NombreArchivoPdf = "ORE-3M-CASCO-H9.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "GUA-ANSELL-HYFLEX",
                Nombre = "GUANTE DE PROTECCIÓN INDUSTRIAL ANSELL HYFLEX 11-800 ANTIESTÁTICO",
                Descripcion = "Proveedor: DISTRIBUIDORA INDUSTRIAL DEL PERÚ S.A.",
                Categoria = "Protección de manos",
                UnidadMedida = "PAR",
                CostoReferencial = 18.00m,
                StockDisponible = 200,
                RutaFichaTecnicaPdf = "/fichas/GUA-ANSELL-HYFLEX.pdf",
                NombreArchivoPdf = "GUA-ANSELL-HYFLEX.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "GUA-CUERO-SOLDADOR",
                Nombre = "GUANTE DE CUERO BADANA Y CARNAZA PARA SOLDADOR MANGA LARGA 16 PULGADAS",
                Descripcion = "Proveedor: IMPORTACIONES MALVINAS S.A.C.",
                Categoria = "Protección de manos",
                UnidadMedida = "PAR",
                CostoReferencial = 22.50m,
                StockDisponible = 85,
                RutaFichaTecnicaPdf = "/fichas/GUA-CUERO-SOLDADOR.pdf",
                NombreArchivoPdf = "GUA-CUERO-SOLDADOR.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "GUA-ANTICORTE-N5",
                Nombre = "GUANTE ANTICORTE NIVEL 5 HPPE CON RECUBRIMIENTO DE POLIURETANO STEELPRO",
                Descripcion = "Proveedor: DISTRIBUIDORA INDUSTRIAL DEL PERÚ S.A.",
                Categoria = "Protección de manos",
                UnidadMedida = "PAR",
                CostoReferencial = 24.00m,
                StockDisponible = 130,
                RutaFichaTecnicaPdf = "/fichas/GUA-ANTICORTE-N5.pdf",
                NombreArchivoPdf = "GUA-ANTICORTE-N5.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "BOT-CAT-HOLT",
                Nombre = "BOTA DE SEGURIDAD DIELÉCTRICA CATERPILLAR HOLT S3 CON PUNTERA COMPOSITE",
                Descripcion = "Proveedor: BATA PERÚ S.A.",
                Categoria = "Protección de pies",
                UnidadMedida = "PAR",
                CostoReferencial = 320.00m,
                StockDisponible = 25,
                RutaFichaTecnicaPdf = "/fichas/BOT-CAT-HOLT.pdf",
                NombreArchivoPdf = "BOT-CAT-HOLT.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "BOT-BATA-ACID",
                Nombre = "BOTA DE JEBE INDUSTRIAL RESISTENTE A HIDROCARBUROS Y ÁCIDOS BATA INDUSTRIAL",
                Descripcion = "Proveedor: BATA PERÚ S.A.",
                Categoria = "Protección de pies",
                UnidadMedida = "PAR",
                CostoReferencial = 65.00m,
                StockDisponible = 50,
                RutaFichaTecnicaPdf = "/fichas/BOT-BATA-ACID.pdf",
                NombreArchivoPdf = "BOT-BATA-ACID.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "TRA-KLEEN-A40",
                Nombre = "MAMELUCO DESCARTABLE KLEENGUARD A40 CON CAPUCHA RESISTENTE A LÍQUIDOS Y POLVOS",
                Descripcion = "Proveedor: DISTRIBUIDORA INDUSTRIAL DEL PERÚ S.A.",
                Categoria = "Protección corporal",
                UnidadMedida = "UND",
                CostoReferencial = 21.00m,
                StockDisponible = 160,
                RutaFichaTecnicaPdf = "/fichas/TRA-KLEEN-A40.pdf",
                NombreArchivoPdf = "TRA-KLEEN-A40.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "MAM-DRIL-REF",
                Nombre = "MAMELUCO INDUSTRIAL EN TELA DRIL TECNOLOGÍA IGNÍFUGA CON CINTA REFLECTIVA 2 PULGADAS",
                Descripcion = "Proveedor: IMPORTACIONES MALVINAS S.A.C.",
                Categoria = "Protección corporal",
                UnidadMedida = "UND",
                CostoReferencial = 78.00m,
                StockDisponible = 45,
                RutaFichaTecnicaPdf = "/fichas/MAM-DRIL-REF.pdf",
                NombreArchivoPdf = "MAM-DRIL-REF.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "MAN-SOLD-CUERO",
                Nombre = "MANDIL DE CUERO CROMO PARA TRABAJOS EN CALIENTE Y SOLDADURA 60X90 CM",
                Descripcion = "Proveedor: IMPORTACIONES MALVINAS S.A.C.",
                Categoria = "Protección corporal",
                UnidadMedida = "UND",
                CostoReferencial = 35.00m,
                StockDisponible = 40,
                RutaFichaTecnicaPdf = "/fichas/MAN-SOLD-CUERO.pdf",
                NombreArchivoPdf = "MAN-SOLD-CUERO.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "LIN-HAWK-AMORT",
                Nombre = "LÍNEA DE VIDA DOBLE TERMINAL CON ABSORBEDOR DE IMPACTO HAWK 1.8M GANCHOS 2 1/4\"",
                Descripcion = "Proveedor: SEGURIDAD INDUSTRIAL TOTAL S.A.C.",
                Categoria = "Trabajo en altura",
                UnidadMedida = "UND",
                CostoReferencial = 135.00m,
                StockDisponible = 30,
                RutaFichaTecnicaPdf = "/fichas/LIN-HAWK-AMORT.pdf",
                NombreArchivoPdf = "LIN-HAWK-AMORT.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "BLO-RET-HAWK-6M",
                Nombre = "BLOQUE RETRÁCTIL DE CABLE DE ACERO GALVANIZADO HAWK 6 METROS ANSI Z359.14",
                Descripcion = "Proveedor: SEGURIDAD INDUSTRIAL TOTAL S.A.C.",
                Categoria = "Trabajo en altura",
                UnidadMedida = "UND",
                CostoReferencial = 410.00m,
                StockDisponible = 15,
                RutaFichaTecnicaPdf = "/fichas/BLO-RET-HAWK-6M.pdf",
                NombreArchivoPdf = "BLO-RET-HAWK-6M.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "MOS-STEEL-50KN",
                Nombre = "MOSQUETÓN DE ACERO CON TRIPLE BLOQUEO AUTOMÁTICO 50 KN CERTIFICADO ANSI",
                Descripcion = "Proveedor: SEGURIDAD INDUSTRIAL TOTAL S.A.C.",
                Categoria = "Trabajo en altura",
                UnidadMedida = "UND",
                CostoReferencial = 38.00m,
                StockDisponible = 70,
                RutaFichaTecnicaPdf = "/fichas/MOS-STEEL-50KN.pdf",
                NombreArchivoPdf = "MOS-STEEL-50KN.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "GUA-QUIM-SOLVEX",
                Nombre = "GUANTE DE NITRILO PARA PROTECCIÓN QUÍMICA ANSELL SOLVEX 37-175 13 PULGADAS",
                Descripcion = "Proveedor: DISTRIBUIDORA INDUSTRIAL DEL PERÚ S.A.",
                Categoria = "Protección de manos",
                UnidadMedida = "PAR",
                CostoReferencial = 14.20m,
                StockDisponible = 110,
                RutaFichaTecnicaPdf = "/fichas/GUA-QUIM-SOLVEX.pdf",
                NombreArchivoPdf = "GUA-QUIM-SOLVEX.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            },
            new Producto
            {
                Sku = "GON-3M-GOGGLE-GEAR",
                Nombre = "GOGGLE DE SEGURIDAD PANORÁMICO 3M GOGGLE GEAR SERIE 500 CON SCOTCHGARD",
                Descripcion = "Proveedor: 3M PERÚ S.A.",
                Categoria = "Protección ocular",
                UnidadMedida = "UND",
                CostoReferencial = 45.00m,
                StockDisponible = 65,
                RutaFichaTecnicaPdf = "/fichas/GON-3M-GOGGLE-GEAR.pdf",
                NombreArchivoPdf = "GON-3M-GOGGLE-GEAR.pdf",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            }
        };

        // Cargar todos los SKUs existentes de golpe para evitar N+1 queries (una sola consulta).
        var existingProducts = await context.Productos
            .Select(p => new { p.Sku, p.RutaImagen, p.Id })
            .ToDictionaryAsync(p => p.Sku, p => p);

        foreach (var prod in productos)
        {
            if (string.IsNullOrWhiteSpace(prod.RutaImagen))
            {
                prod.RutaImagen = $"/images/productos/{prod.Sku}.png";
            }

            if (!existingProducts.TryGetValue(prod.Sku, out var existing))
            {
                context.Productos.Add(prod);
            }
            else if (string.IsNullOrWhiteSpace(existing.RutaImagen))
            {
                var tracked = await context.Productos.FindAsync(existing.Id);
                if (tracked != null) tracked.RutaImagen = prod.RutaImagen;
            }
        }

        await context.SaveChangesAsync();

        var proveedoresRegistrados = await context.Proveedores.AsNoTracking().ToListAsync();
        var productosConProveedores = await context.Productos
            .Include(p => p.ProveedoresAutorizados)
            .ToListAsync();
        foreach (var producto in productosConProveedores.Where(p => p.ProveedoresAutorizados.Count == 0))
        {
            var nombres = producto.Descripcion
                .Split("Proveedor:", StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()?
                .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
            var proveedoresAsignados = nombres
                .Select(nombre => proveedoresRegistrados.FirstOrDefault(proveedor =>
                    proveedor.RazonSocial.Contains(nombre, StringComparison.OrdinalIgnoreCase)
                    || nombre.Contains(proveedor.RazonSocial, StringComparison.OrdinalIgnoreCase)))
                .Where(proveedor => proveedor != null)
                .DistinctBy(proveedor => proveedor!.Id)
                .ToList();

            for (var index = 0; index < proveedoresAsignados.Count; index++)
            {
                var proveedor = proveedoresAsignados[index]!;
                producto.ProveedoresAutorizados.Add(new ProductoProveedor
                {
                    ProveedorId = proveedor.Id,
                    CostoCompra = producto.CostoReferencial,
                    EsPrincipal = index == 0,
                    PlazoEntregaHoras = 24
                });
            }
        }
        await context.SaveChangesAsync();

        // 3. Semillas de Cotizaciones Aprobadas y Órdenes de Venta Confirmadas (Año y Fechas Actuales)
        if (!await context.Cotizaciones.AnyAsync())
        {
            var clienteMinera = await context.Clientes.FirstOrDefaultAsync(c => c.Ruc == "20601234567");
            var clientePacifico = await context.Clientes.FirstOrDefaultAsync(c => c.Ruc == "20548912340");
            var clienteVial = await context.Clientes.FirstOrDefaultAsync(c => c.Ruc == "20492817263");
            var casco = await context.Productos.FirstOrDefaultAsync(p => p.Sku == "CAS-3M-H700");
            var lentes = await context.Productos.FirstOrDefaultAsync(p => p.Sku == "LEN-3M-VIRTUA");
            var respirador = await context.Productos.FirstOrDefaultAsync(p => p.Sku == "RES-3M-6200");
            var prov3M = await context.Proveedores.FirstOrDefaultAsync(p => p.Ruc == "20100138056");

            var hoy = DateTime.UtcNow;
            var currentYear = hoy.Year;

            if (clienteVial != null && casco != null && lentes != null)
            {
                var cot1 = new Cotizacion
                {
                    Correlativo = $"COT-{currentYear}-0842",
                    ClienteId = clienteVial.Id,
                    FechaEmision = hoy.AddDays(-2),
                    FechaVencimiento = hoy.AddDays(28),
                    Estado = "Aprobada",
                    CondicionPago = "Crédito 30 días",
                    Subtotal = 4200.00m,
                    Igv = 756.00m,
                    Total = 4956.00m
                };
                context.Cotizaciones.Add(cot1);
                await context.SaveChangesAsync();

                var ov1 = new OrdenVenta
                {
                    CotizacionId = cot1.Id,
                    FechaCreacion = hoy.AddDays(-2).AddHours(2),
                    EstadoLogistico = "EnPreparacion"
                };
                context.OrdenesVenta.Add(ov1);
                await context.SaveChangesAsync();

                if (prov3M != null)
                {
                    var oc1 = new OrdenCompra
                    {
                        ProveedorId = prov3M.Id,
                        OrdenVentaId = ov1.Id,
                        FechaCompra = hoy.AddDays(-1),
                        Estado = "Recibida",
                        Total = 3200.00m
                    };
                    context.OrdenesCompra.Add(oc1);
                    await context.SaveChangesAsync();
                }
            }

            if (clienteMinera != null && respirador != null)
            {
                var cot2 = new Cotizacion
                {
                    Correlativo = $"COT-{currentYear}-0839",
                    ClienteId = clienteMinera.Id,
                    FechaEmision = hoy.AddDays(-2),
                    FechaVencimiento = hoy.AddDays(15),
                    Estado = "Aprobada",
                    CondicionPago = "Crédito 15 días",
                    Subtotal = 3850.00m,
                    Igv = 693.00m,
                    Total = 4543.00m
                };
                context.Cotizaciones.Add(cot2);
                await context.SaveChangesAsync();

                var ov2 = new OrdenVenta
                {
                    CotizacionId = cot2.Id,
                    FechaCreacion = hoy.AddDays(-2).AddHours(4),
                    EstadoLogistico = "EnPreparacion"
                };
                context.OrdenesVenta.Add(ov2);
                await context.SaveChangesAsync();
            }

            if (clientePacifico != null && casco != null)
            {
                var cot3 = new Cotizacion
                {
                    Correlativo = $"COT-{currentYear}-0824",
                    ClienteId = clientePacifico.Id,
                    FechaEmision = hoy.AddDays(-4),
                    FechaVencimiento = hoy.AddDays(10),
                    Estado = "Aprobada",
                    CondicionPago = "Crédito 15 días",
                    Subtotal = 5100.00m,
                    Igv = 918.00m,
                    Total = 6018.00m
                };
                context.Cotizaciones.Add(cot3);
                await context.SaveChangesAsync();

                var ov3 = new OrdenVenta
                {
                    CotizacionId = cot3.Id,
                    FechaCreacion = hoy.AddDays(-4).AddHours(3),
                    EstadoLogistico = "Despachado"
                };
                context.OrdenesVenta.Add(ov3);
                await context.SaveChangesAsync();
            }
        }
    }
}
