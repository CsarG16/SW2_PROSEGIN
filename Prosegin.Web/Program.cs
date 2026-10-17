// PROSEGIN Core - Inicialización de la aplicación Web
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Buscar el archivo .env desde el proyecto hacia la raíz del repositorio/solución.
var directory = new DirectoryInfo(builder.Environment.ContentRootPath);
string? envFilePath = null;
while (directory != null)
{
    var candidatePath = Path.Combine(directory.FullName, ".env");
    if (File.Exists(candidatePath))
    {
        envFilePath = candidatePath;
        break;
    }

    directory = directory.Parent;
}

if (envFilePath != null)
{
    foreach (var line in File.ReadAllLines(envFilePath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex > 0)
        {
            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

var connectionArgIndex = Array.FindIndex(args, a => a.Equals("--connection", StringComparison.OrdinalIgnoreCase));
if (connectionArgIndex >= 0 && connectionArgIndex + 1 < args.Length)
{
    var overrideConn = args[connectionArgIndex + 1];
    builder.Configuration["ConnectionStrings:DefaultConnection"] = overrideConn;
    Environment.SetEnvironmentVariable("DB_HOST", null);
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDataServices(builder.Configuration);

// Servicio de integración SUNAT con IHttpClientFactory
builder.Services.AddHttpClient("SunatClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(4);
});
builder.Services.AddScoped<ISunatService, SunatService>();
builder.Services.AddScoped<IPedidosService, PedidosService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// Infraestructura de Conexión Nubefact (Historia Técnica HT-03)
builder.Services.Configure<Prosegin.Web.Services.Nubefact.NubefactSettings>(
    builder.Configuration.GetSection(Prosegin.Web.Services.Nubefact.NubefactSettings.SectionName));

builder.Services.AddHttpClient<Prosegin.Web.Services.Nubefact.INubefactService, Prosegin.Web.Services.Nubefact.NubefactService>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Prosegin.Web.Services.Nubefact.NubefactSettings>>().Value;
    if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
    {
        client.BaseAddress = new Uri(settings.BaseUrl);
    }
    if (!string.IsNullOrWhiteSpace(settings.Token))
    {
        // En Nubefact el token se envía directo sin la palabra "Bearer"
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", settings.Token.Trim());
    }
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 10);
});

// Infraestructura de Almacenamiento en la Nube Supabase (Historia Técnica HT-02)
builder.Services.Configure<Prosegin.Web.Services.Supabase.SupabaseSettings>(
    builder.Configuration.GetSection(Prosegin.Web.Services.Supabase.SupabaseSettings.SectionName));

builder.Services.AddHttpClient<Prosegin.Web.Services.Supabase.ISupabaseStorageService, Prosegin.Web.Services.Supabase.SupabaseStorageService>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Prosegin.Web.Services.Supabase.SupabaseSettings>>().Value;
    if (!string.IsNullOrWhiteSpace(settings.Url) && Uri.TryCreate(settings.Url, UriKind.Absolute, out var uri))
    {
        client.BaseAddress = uri;
    }
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Configuración de localización para moneda y números en Perú (acepta punto y coma decimal)
var defaultCulture = new System.Globalization.CultureInfo("es-PE");
defaultCulture.NumberFormat.NumberDecimalSeparator = ".";
defaultCulture.NumberFormat.CurrencyDecimalSeparator = ".";

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(defaultCulture),
    SupportedCultures = new[] { defaultCulture },
    SupportedUICultures = new[] { defaultCulture }
};
app.UseRequestLocalization(localizationOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Modo CLI para sembrado directo de datos si se pasa el flag --seed
if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("[PROSEGIN SEEDER] Aplicando migraciones pendientes...");
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        await context.Database.MigrateAsync();
    }

    Console.WriteLine("[PROSEGIN SEEDER] Sembrando datos de Clientes...");
    await ClienteDataSeeder.SeedAsync(app.Services);

    Console.WriteLine("[PROSEGIN SEEDER] Sembrando Proveedores, Productos y Tarifas...");
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        await DbSeeder.SeedAsync(context);
    }

    Console.WriteLine("[PROSEGIN SEEDER] ¡Datos iniciales sembrados con éxito en la base de datos!");
    return;
}

// Aplicar cambios de esquema antes de consultar o sembrar datos.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
    await context.Database.MigrateAsync();
}

// Sembrado de datos de clientes si no existen
await ClienteDataSeeder.SeedAsync(app.Services);

// Ejecución de Seeder de datos iniciales de EPP y proveedores
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ProseginDbContext>();
        await DbSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al ejecutar el seeder de base de datos.");
    }
}

app.Run();
