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

        var passwordEntorno = Environment.GetEnvironmentVariable("PROSEGIN_ADMIN_PASSWORD");

        var adminExistente = await context.CuentasPersonal.FirstOrDefaultAsync(c => c.NombreUsuario == "admin");
        if (adminExistente != null)
        {
            // Solo se restablece la clave si se indica explícitamente por variable de entorno;
            // así no se pisan las contraseñas cambiadas desde el módulo de cuentas.
            if (!string.IsNullOrWhiteSpace(passwordEntorno))
            {
                adminExistente.PasswordHash = PasswordHashService.Hash(passwordEntorno);
                adminExistente.Activa = true;
                await context.SaveChangesAsync();
            }
            return;
        }

        if (await context.CuentasPersonal.AnyAsync())
            return;

        var password = !string.IsNullOrWhiteSpace(passwordEntorno) ? passwordEntorno : "1234";

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
