# 🎨 PROSEGIN UI - Sistema de Diseño Estandarizado (V2)

Este documento define el **Sistema de Diseño (Design System)** oficial de **PROSEGIN Core** para garantizar que todos los desarrolladores y la IA construyan pantallas consistentes, limpias y profesionales.

---

## 🎨 0. Color de Acento Central (Cambiar Todo el Sistema)

Para cambiar el color corporativo de todo el sistema, solo modifica estas variables en `:root` de `site.css`:

```css
:root {
  --accent: #f59e0b;        /* Color principal (Dorado/Ámbar) */
  --accent-hover: #d97706;  /* Hover */
  --accent-light: #fef3c7;  /* Fondo sutil */
  --accent-text: #b45309;   /* Texto sobre fondo sutil */
  --accent-on: #0f172a;     /* Texto SOBRE el botón de acento */
  --accent-shadow: rgba(245, 158, 11, 0.30);
}
```

> Cambiar estas 6 líneas actualiza automáticamente: botones primarios, botones de acción en tablas, badges, focus de inputs, alertas y encabezados.

---

## 1. 📦 Componentes Reutilizables (Copy-Paste)

### A. Encabezado de Página (`.prosegin-page-header--accent`)
Toda vista DEBE iniciar con un encabezado minimalista con acento lateral dorado:

```html
<div class="prosegin-card p-3 p-md-4 mb-3">
    <div class="prosegin-page-header--accent" style="margin-bottom: 0;">
        <div class="header-content">
            <h1 class="page-title">Directorio de Clientes</h1>
            <p class="page-subtitle">Gestión comercial y verificación SUNAT</p>
        </div>
        <a asp-action="Create" class="btn-prosegin-primary">
            + REGISTRAR NUEVO CLIENTE
        </a>
    </div>
</div>
```

**PROHIBIDO:** Banners gigantes con texto "SISTEMA COMPRA Y VENTA...", fondos recargados o sombras estridentes.

---

### B. Botones de Acción

```html
<!-- Botón Principal (Dorado Corporativo — para Buscar, Guardar, Registrar) -->
<button class="btn-prosegin-primary">
    <i class="bi bi-search"></i> BUSCAR
</button>

<!-- Botón Secundario (Blanco con borde — para Cancelar, Volver) -->
<a href="#" class="btn-prosegin-secondary">
    <i class="bi bi-arrow-left"></i> Cancelar
</a>

<!-- Botón de Acción en Fila de Tabla (Dorado — para Cotizar, Editar) -->
<a href="#" class="btn-prosegin-action">
    <i class="bi bi-file-earmark-text"></i> Cotizar
</a>

<!-- Botón Secundario en Fila (Gris suave — para Sedes, Detalles) -->
<a href="#" class="btn-prosegin-action-secondary">
    <i class="bi bi-building"></i> Sedes (3)
</a>

<!-- Botón Oscuro Midnight (Opcional — para acciones especiales) -->
<button class="btn-prosegin-dark">
    <i class="bi bi-printer"></i> Imprimir Rótulo
</button>
```

| Clase CSS | Color | Uso |
| :--- | :--- | :--- |
| `.btn-prosegin-primary` | **Dorado** (`--accent`) | Crear, Guardar, Buscar, Confirmar |
| `.btn-prosegin-secondary` | Blanco con borde | Cancelar, Volver, Limpiar |
| `.btn-prosegin-action` | **Dorado** (`--accent`) | Acción en fila de tabla (Cotizar) |
| `.btn-prosegin-action-secondary` | Gris suave | Acción secundaria en tabla (Sedes) |
| `.btn-prosegin-dark` | Midnight Navy (`--dark`) | Acciones especiales |

---

### C. Badges de Estado (No interactivos, con Dot Indicator)
Los estados **no deben parecer botones clickeables**. Usar siempre `.badge-status` con `.status-dot`:

```html
<!-- Activo + Habido (verde) -->
<span class="badge-status badge-status-success">
    <span class="status-dot"></span> ACTIVO · HABIDO
</span>

<!-- Neutral / Pendiente -->
<span class="badge-status badge-status-neutral">
    <span class="status-dot"></span> NO HABIDO
</span>

<!-- Peligro / Bloqueado -->
<span class="badge-status badge-status-danger">
    <span class="status-dot"></span> BLOQUEADO
</span>

<!-- Advertencia / En Proceso -->
<span class="badge-status badge-status-warning">
    <span class="status-dot"></span> PENDIENTE
</span>
```

---

### D. Caja de Búsqueda (`.prosegin-search-box`)

```html
<div class="prosegin-search-box">
    <i class="bi bi-search"></i>
    <input type="text" name="termino" placeholder="Ingrese RUC o razón social..." />
</div>
```

---

### E. Tabla Estándar (`.prosegin-table`)

```html
<div class="prosegin-card overflow-hidden">
    <!-- Encabezado de Resultados -->
    <div class="prosegin-results-header">
        <div class="d-flex align-items-center gap-2">
            <span class="results-title">Clientes Encontrados</span>
            <span class="count-pill">3 registros</span>
        </div>
        <span class="results-note">Datos validados con Padrón SUNAT</span>
    </div>

    <!-- Tabla -->
    <div class="table-responsive">
        <table class="prosegin-table mb-0">
            <thead>
                <tr>
                    <th>EMPRESA</th>
                    <th>RUC</th>
                    <th class="text-end">ACCIONES</th>
                </tr>
            </thead>
            <tbody>
                <tr>
                    <td>
                        <div class="d-flex align-items-center gap-3">
                            <div class="row-icon-avatar"><i class="bi bi-building"></i></div>
                            <div>
                                <div class="fw-bold text-dark">CONSTRUCTORA DEL PACIFICO S.A.C.</div>
                                <div class="text-muted" style="font-size: 0.78rem;">Cliente Habitual</div>
                            </div>
                        </div>
                    </td>
                    <td>20548912340</td>
                    <td class="text-end">
                        <a href="#" class="btn-prosegin-action-secondary">Sedes (2)</a>
                        <a href="#" class="btn-prosegin-action">Cotizar</a>
                    </td>
                </tr>
            </tbody>
        </table>
    </div>

    <!-- Pie -->
    <div class="prosegin-table-footer">
        <span>Mostrando 3 de 3 registros</span>
    </div>
</div>
```

---

### F. Alertas Estándar (`.prosegin-alert`)

```html
<div class="prosegin-alert prosegin-alert-warning">
    <i class="bi bi-exclamation-triangle-fill"></i>
    <span>No se encontraron clientes registrados con los datos ingresados</span>
</div>

<div class="prosegin-alert prosegin-alert-danger">
    <i class="bi bi-exclamation-octagon-fill"></i>
    <span>El cliente tiene facturas vencidas.</span>
</div>

<div class="prosegin-alert prosegin-alert-success">
    <i class="bi bi-check-circle-fill"></i>
    <span>Cliente registrado exitosamente.</span>
</div>
```

---

### G. Estado Vacío (`.prosegin-empty-state`)

```html
<div class="prosegin-card">
    <div class="prosegin-empty-state">
        <i class="bi bi-building-x empty-icon d-block"></i>
        <div class="empty-title">No se encontraron resultados</div>
        <div class="empty-desc">Verifique los datos ingresados o registre un nuevo cliente.</div>
        <a href="#" class="btn-prosegin-primary">
            <i class="bi bi-person-plus"></i> Registrar cliente
        </a>
    </div>
</div>
```

---

## 2. 🌈 Paleta Completa de Variables CSS

| Elemento | Variable CSS | Color |
| :--- | :--- | :--- |
| **Acento Principal (Dorado)** | `var(--accent)` | `#f59e0b` |
| **Acento Hover** | `var(--accent-hover)` | `#d97706` |
| **Acento Fondo Sutil** | `var(--accent-light)` | `#fef3c7` |
| **Midnight Navy (Dark)** | `var(--dark)` | `#0f1b2b` |
| **Fondo Canvas** | `var(--bg-canvas)` | `#f8fafc` |
| **Fondo de Tarjetas** | `var(--bg-card)` | `#ffffff` |
| **Texto Principal** | `var(--text-dark)` | `#0f172a` |
| **Texto Secundario** | `var(--text-muted)` | `#64748b` |
| **Borde Estándar** | `var(--border-card)` | `#e2e8f0` |
| **Éxito** | `var(--state-success)` | `#16a34a` |
| **Peligro** | `var(--state-danger)` | `#ef4444` |
| **Advertencia** | `var(--state-warning)` | `#f59e0b` |

---

## 3. 🧭 Estructura del Menú Lateral (Sidebar)

Los estilos del menú lateral y layout se encuentran desacoplados en `wwwroot/css/sidebar.css`, y su marcado HTML está centralizado en `Views/Shared/_Layout.cshtml`.

Cuenta con los módulos oficiales:
1. `Catálogo de EPP`
2. `Búsqueda de Clientes`
3. `Edición de Precios`
4. `Gestión de Pedidos`
5. `Datos Tributarios` (actualmente en `/Clientes/Create`)
