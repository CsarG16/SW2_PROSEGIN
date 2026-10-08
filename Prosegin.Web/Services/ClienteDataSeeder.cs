using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;

namespace Prosegin.Web.Services;

public static class ClienteDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();

            // 1. CONSTRUCTORA E INGENIERÍA MINERA DEL SUR S.A.C.
            var c1 = await context.Clientes.Include(c => c.PuntosEntrega).FirstOrDefaultAsync(c => c.Ruc == "20601234567");
            if (c1 == null)
            {
                c1 = new Cliente
                {
                    Ruc = "20601234567",
                    RazonSocial = "CONSTRUCTORA E INGENIERÍA MINERA DEL SUR S.A.C.",
                    DireccionFiscal = "Av. Las Begonias Nro. 441, San Isidro - Lima",
                    Departamento = "Lima",
                    Provincia = "Lima",
                    Distrito = "San Isidro",
                    Ubigeo = "150131",
                    EstadoSunat = "ACTIVO",
                    CondicionSunat = "HABIDO",
                    Telefono = "014218900",
                    CorreoElectronico = "operaciones@minerasur.pe",
                    FechaCreacion = DateTime.UtcNow.AddMonths(-6),
                    Activo = true
                };
                context.Clientes.Add(c1);
                await context.SaveChangesAsync();

                context.PuntosEntrega.AddRange(
                    new PuntoEntrega
                    {
                        ClienteId = c1.Id,
                        TipoSede = "Sede Central / Oficina",
                        NombreAlias = "Sede San Isidro",
                        Direccion = "Av. Las Begonias Nro. 441, San Isidro - Lima",
                        Departamento = "Lima",
                        Provincia = "Lima",
                        Distrito = "San Isidro",
                        Ubigeo = "150131",
                        EsPredeterminada = true,
                        Activo = true
                    },
                    new PuntoEntrega
                    {
                        ClienteId = c1.Id,
                        TipoSede = "Mina / Campamento",
                        NombreAlias = "Campamento Minero Sur",
                        Direccion = "Carretera Panamericana Sur Km 450",
                        Departamento = "Ica",
                        Provincia = "Nazca",
                        Distrito = "Marcona",
                        Ubigeo = "110302",
                        EsPredeterminada = false,
                        Activo = true
                    },
                    new PuntoEntrega
                    {
                        ClienteId = c1.Id,
                        TipoSede = "Almacén Central",
                        NombreAlias = "Almacén Logístico Lurín",
                        Direccion = "Av. Industrial Mz. B Lote 4, Lurín",
                        Departamento = "Lima",
                        Provincia = "Lima",
                        Distrito = "Lurín",
                        Ubigeo = "150119",
                        EsPredeterminada = false,
                        Activo = true
                    }
                );
                await context.SaveChangesAsync();
            }

            // 2. CONSTRUCTORA DEL PACIFICO S.A.C.
            var c2 = await context.Clientes.Include(c => c.PuntosEntrega).FirstOrDefaultAsync(c => c.Ruc == "20548912340");
            if (c2 == null)
            {
                c2 = new Cliente
                {
                    Ruc = "20548912340",
                    RazonSocial = "CONSTRUCTORA DEL PACIFICO S.A.C.",
                    DireccionFiscal = "Av. Industrial 1450, Ate - Lima",
                    Departamento = "Lima",
                    Provincia = "Lima",
                    Distrito = "Ate",
                    Ubigeo = "150103",
                    EstadoSunat = "ACTIVO",
                    CondicionSunat = "HABIDO",
                    Telefono = "013567890",
                    CorreoElectronico = "contacto@pacificoconstructora.pe",
                    FechaCreacion = DateTime.UtcNow.AddMonths(-4),
                    Activo = true
                };
                context.Clientes.Add(c2);
                await context.SaveChangesAsync();

                context.PuntosEntrega.AddRange(
                    new PuntoEntrega
                    {
                        ClienteId = c2.Id,
                        TipoSede = "Sede Central / Oficina",
                        NombreAlias = "Sede Principal Ate",
                        Direccion = "Av. Industrial 1450, Ate - Lima",
                        Departamento = "Lima",
                        Provincia = "Lima",
                        Distrito = "Ate",
                        Ubigeo = "150103",
                        EsPredeterminada = true,
                        Activo = true
                    },
                    new PuntoEntrega
                    {
                        ClienteId = c2.Id,
                        TipoSede = "Almacén Secundario",
                        NombreAlias = "Planta Industrial Callao",
                        Direccion = "Av. Néstor Gambetta 1200, Callao",
                        Departamento = "Callao",
                        Provincia = "Callao",
                        Distrito = "Callao",
                        Ubigeo = "070101",
                        EsPredeterminada = false,
                        Activo = true
                    }
                );
                await context.SaveChangesAsync();
            }

            // 3. CONSORCIO VIAL ANDINO S.R.L.
            var c3 = await context.Clientes.Include(c => c.PuntosEntrega).FirstOrDefaultAsync(c => c.Ruc == "20492817263");
            if (c3 == null)
            {
                c3 = new Cliente
                {
                    Ruc = "20492817263",
                    RazonSocial = "CONSORCIO VIAL ANDINO S.R.L.",
                    DireccionFiscal = "Jr. Huancavelica 320, Cercado - Lima",
                    Departamento = "Lima",
                    Provincia = "Lima",
                    Distrito = "Lima",
                    Ubigeo = "150101",
                    EstadoSunat = "ACTIVO",
                    CondicionSunat = "HABIDO",
                    Telefono = "014283921",
                    CorreoElectronico = "licitaciones@vialandino.pe",
                    FechaCreacion = DateTime.UtcNow.AddMonths(-2),
                    Activo = true
                };
                context.Clientes.Add(c3);
                await context.SaveChangesAsync();

                context.PuntosEntrega.Add(
                    new PuntoEntrega
                    {
                        ClienteId = c3.Id,
                        TipoSede = "Sede Central / Oficina",
                        NombreAlias = "Sede Central Cercado",
                        Direccion = "Jr. Huancavelica 320, Cercado - Lima",
                        Departamento = "Lima",
                        Provincia = "Lima",
                        Distrito = "Lima",
                        Ubigeo = "150101",
                        EsPredeterminada = true,
                        Activo = true
                    }
                );
                await context.SaveChangesAsync();
            }
        }
        catch
        {
            // Evitar interrupción si la base de datos se encuentra en migración inicial
        }
    }
}
