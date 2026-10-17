// PROSEGIN Core - Inicialización de la aplicación Web
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

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

if (envFilePath is not null)
{
    try
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
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }
    catch (Exception ex)
    {
        var logger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("Program");
        logger.LogWarning(ex, "No se pudo cargar el archivo .env desde {EnvFilePath}", envFilePath);
    }
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "ProseginAuth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Ejecución de Seeder de datos iniciales de EPP y proveedores
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ProseginDbContext>();
        context.Database.EnsureCreated();
        await ClienteDataSeeder.SeedAsync(app.Services);
        await DbSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al ejecutar el seeder de base de datos.");
    }
}

app.Run();
