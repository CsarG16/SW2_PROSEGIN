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

        var password = Environment.GetEnvironmentVariable("PROSEGIN_ADMIN_PASSWORD") ?? "1234";

        var adminExistente = await context.CuentasPersonal.FirstOrDefaultAsync(c => c.NombreUsuario == "admin");
        if (adminExistente != null)
        {
            adminExistente.PasswordHash = PasswordHashService.Hash(password);
            adminExistente.Activa = true;
            await context.SaveChangesAsync();
            return;
        }

        if (await context.CuentasPersonal.AnyAsync())
            return;

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
