using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;

namespace Prosegin.Web.Services;

public static class CuentaPersonalDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        if (await context.CuentasPersonal.AnyAsync())
            return;

        var password = Environment.GetEnvironmentVariable("PROSEGIN_ADMIN_PASSWORD") ?? "Prosegin#2026";
        context.CuentasPersonal.Add(new CuentaPersonal
        {
            NombreCompleto = "Administrador PROSEGIN",
            NombreUsuario = "admin",
            CorreoElectronico = "admin@prosegin.com",
            Rol = "Administrador",
            PasswordHash = PasswordHashService.Hash(password),
            Activa = true
        });
        await context.SaveChangesAsync();
    }
}
