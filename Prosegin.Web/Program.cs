using Prosegin.Data;
using Prosegin.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Temporalmente desactivado - usar solo appsettings.json para desarrollo local
// Cargar variables de entorno desde el archivo .env en la raíz de la solución
// var rootDirectory = Directory.GetParent(builder.Environment.ContentRootPath)?.FullName ?? builder.Environment.ContentRootPath;
// var envFilePath = Path.Combine(rootDirectory, ".env");
// if (!File.Exists(envFilePath))
// {
//     envFilePath = Path.Combine(builder.Environment.ContentRootPath, ".env");
// }

// if (File.Exists(envFilePath))
// {
//     foreach (var line in File.ReadAllLines(envFilePath))
//     {
//         var trimmed = line.Trim();
//         if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
//         var separatorIndex = trimmed.IndexOf('=');
//         if (separatorIndex > 0)
//         {
//             var key = trimmed[..separatorIndex].Trim();
//             var value = trimmed[(separatorIndex + 1)..].Trim();
//             Environment.SetEnvironmentVariable(key, value);
//         }
//     }
// }

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDataServices(builder.Configuration);

// Servicio de integración SUNAT con IHttpClientFactory
builder.Services.AddHttpClient("SunatClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(4);
});
builder.Services.AddScoped<ISunatService, SunatService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
