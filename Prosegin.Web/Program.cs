// PROSEGIN Core - Inicialización de la aplicación Web
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Prosegin.Data;
using Prosegin.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Si no hay variables de entorno SMTP_*, se toman de la sección "Smtp" de appsettings.json.
foreach (var (variable, clave) in new[]
{
    ("SMTP_HOST", "Host"), ("SMTP_PORT", "Port"), ("SMTP_USER", "User"),
    ("SMTP_PASSWORD", "Password"), ("SMTP_FROM", "From"), ("SMTP_SSL", "Ssl")
})
{
    var valor = builder.Configuration["Smtp:" + clave];
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)) && !string.IsNullOrWhiteSpace(valor))
        Environment.SetEnvironmentVariable(variable, valor);
}

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

// No exponer el encabezado "Server: Kestrel".
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Cookie antiforgery endurecida (HttpOnly, SameSite y Secure fuera de desarrollo).
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Límite de peticiones al inicio de sesión (mitiga fuerza bruta): 10 intentos por minuto y por IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".Prosegin.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/Acceso/Login";
        options.AccessDeniedPath = "/Acceso/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var userName = context.Principal?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ProseginDbContext>();
            var activa = await db.CuentasPersonal
                .AsNoTracking()
                .AnyAsync(c => c.NombreUsuario == userName && c.Activa);
            if (!activa)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddScoped<ICuentasPersonalService, CuentasPersonalService>();

builder.Services.AddHttpClient("ComprobantesClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

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

// Cabeceras de seguridad para todas las respuestas.
var esDesarrollo = app.Environment.IsDevelopment();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "img-src 'self' data: blob: https:; " +
        "connect-src 'self' https:" + (esDesarrollo ? " ws: wss: http://localhost:*" : string.Empty) + "; " +
        "frame-src 'self' blob: https:; " +
        "object-src 'self' blob: https:; " +
        "base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
    await next();
});

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
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

    await CuentaPersonalDataSeeder.SeedAsync(app.Services);

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
await CuentaPersonalDataSeeder.SeedAsync(app.Services);

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
