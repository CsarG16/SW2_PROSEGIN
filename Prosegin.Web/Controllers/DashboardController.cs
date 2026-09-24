using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;

namespace Prosegin.Web.Controllers;

public class DashboardController : Controller
{
    private readonly ProseginDbContext _context;

    public DashboardController(ProseginDbContext context)
    {
        _context = context;
    }

    // GET: Dashboard
    public async Task<IActionResult> Index()
    {
        // KPIs
        var totalProductos = await _context.Productos.CountAsync(p => p.Activo);
        var totalClientes = await _context.Clientes.CountAsync(c => c.Activo);
        var stockBajo = await _context.Productos.CountAsync(p => p.Activo && p.Stock < 10);
        var productosSinStock = await _context.Productos.CountAsync(p => p.Activo && p.Stock == 0);

        // Productos por categoría
        var productosPorCategoria = await _context.Productos
            .Where(p => p.Activo)
            .GroupBy(p => p.Categoria)
            .Select(g => new { Categoria = g.Key, Cantidad = g.Count() })
            .OrderByDescending(g => g.Cantidad)
            .ToListAsync();

        // Clientes recientes
        var clientesRecientes = await _context.Clientes
            .Where(c => c.Activo)
            .OrderByDescending(c => c.FechaCreacion)
            .Take(5)
            .ToListAsync();

        // Productos con stock crítico
        var productosStockCritico = await _context.Productos
            .Where(p => p.Activo && p.Stock < 10)
            .OrderBy(p => p.Stock)
            .Take(5)
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            TotalProductos = totalProductos,
            TotalClientes = totalClientes,
            StockBajo = stockBajo,
            ProductosSinStock = productosSinStock,
            ProductosPorCategoria = productosPorCategoria.ToDictionary(x => x.Categoria, x => x.Cantidad),
            ClientesRecientes = clientesRecientes,
            ProductosStockCritico = productosStockCritico
        };

        return View(viewModel);
    }
}

public class DashboardViewModel
{
    public int TotalProductos { get; set; }
    public int TotalClientes { get; set; }
    public int StockBajo { get; set; }
    public int ProductosSinStock { get; set; }
    public Dictionary<string, int> ProductosPorCategoria { get; set; } = new();
    public List<Cliente> ClientesRecientes { get; set; } = new();
    public List<Producto> ProductosStockCritico { get; set; } = new();
}