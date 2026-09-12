# Antigravity Rules - PROSEGIN CORE (TRIVILIN EPP)

Este repositorio contiene la solución empresarial **Prosegin Core**, un sistema web para la gestión de compras y ventas de Equipos de Protección Personal (EPP) bajo un modelo "Just in Time" en el conglomerado comercial de Las Malvinas (Lima, Perú).

El contexto completo y detallado del negocio, procesos AS-IS vs TO-BE y mapa de módulos se encuentra en:
👉 [Contexto Completo del Proyecto](file:///c:/Users/Jualo/Desktop/TRIVILIN%20EPP/.agents/rules/project_context.md)

---

## 🏛️ Arquitectura del Sistema (.NET 8 - 2 Proyectos Pragmáticos)
1. **`Prosegin.Data`**:
   - Capa central de datos y entidades.
   - Contiene: Entidades de negocio (`Entities/`), `ProseginDbContext` de EF Core, migraciones y configuración de conexión MySQL con Pomelo.
2. **`Prosegin.Web`**:
   - Capa de presentación y lógica de negocio (ASP.NET Core MVC con Razor Views).
   - Contiene:
     - `Services/`: Lógica de negocio (cálculo de margen, validación de clientes morosos, anexado de PDFs).
     - `Controllers/`, `Views/`, `ViewModels/`: Pantallas e interacción con el usuario.
     - `Program.cs`: Configuración de inicio, carga de `.env` e Inyección de Dependencias.

---

## ⚡ Reglas Clave de Negocio para la IA
1. **Modelo Comercial:**
   - Compra al contado/efectivo en Las Malvinas; venta a crédito corporativo a clientes (7, 15, 30 días).
2. **Fórmula Obligatoria de Precio de Venta:**
   $$\text{Precio Venta} = \frac{\text{Costo Proveedor}}{1 - \text{Margen Deseado}}$$
   - Validar y calcular siempre en servidor antes de guardar.
3. **Gestión de Fichas Técnicas:**
   - Los archivos PDF se almacenan físicamente en el servidor (ej. `wwwroot/uploads/fichas/`). En MySQL se persiste únicamente la ruta relativa y metadatos.
   - La cotización en PDF debe incluir/anexar automáticamente las fichas técnicas de los productos cotizados.
4. **Protección Contra Clientes Morosos:**
   - Antes de registrar una cotización o venta a crédito, validar si el cliente tiene facturas impagas que superaron su fecha de vencimiento.
5. **Comprobantes Electrónicos:**
   - Preparado para emisión automatizada de Factura Electrónica y Guía de Remisión Electrónica Remitente (GRE) sin doble digitación.

---

## 💻 Convenciones de Código C#
- Nomenclatura en C#: PascalCase para Clases, Métodos y Propiedades; camelCase para parámetros y `_camelCase` para campos privados inyectados.
- Tipado fuerte, métodos asíncronos (`async`/`await`) con `CancellationToken` en operaciones de I/O y base de datos.
- Validaciones en capa Core / Servicios antes de persistir cambios.
