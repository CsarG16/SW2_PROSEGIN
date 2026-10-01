// PROSEGIN Core - Inicialización de la aplicación Web
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
// prosegin core
