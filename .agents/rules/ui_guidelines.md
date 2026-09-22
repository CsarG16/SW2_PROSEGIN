# 🎨 PROSEGIN UI - Guía de Estilos y Componentes para el Equipo

Esta regla define el **Sistema de Diseño (Design System)** oficial de **PROSEGIN Core** para garantizar que los desarrolladores y la IA construyan pantallas consistentes, modernas y alineadas a la imagen corporativa del negocio.

---

## 1. 🌈 Paleta de Colores y Variables CSS (`site.css`)

Utilizar siempre las variables CSS en lugar de colores quemados:

| Elemento / Propósito | Variable CSS | Color / Hex | Muestra |
| :--- | :--- | :--- | :--- |
| **Naranja Corporativo (Principal)** | `var(--prosegin-orange)` | `#f59e0b` | Botones de acción, logos, acentos activos |
| **Naranja Hover** | `var(--prosegin-orange-hover)` | `#d97706` | Efecto hover de botones primarios |
| **Fondo Sidebar (Oscuro)** | `var(--sidebar-bg)` | `#0c1322` | Menú lateral izquierdo |
| **Fondo Canvas (Claro)** | `var(--bg-canvas)` | `#f4f6f9` | Fondo general de todas las vistas |
| **Fondo de Tarjetas** | `var(--bg-card)` | `#ffffff` | Superficie de paneles y formularios |
| **Texto Principal** | `var(--text-dark)` | `#0f172a` | Encabezados y títulos principales |
| **Texto Secundario** | `var(--text-muted)` | `#64748b` | Subtítulos, labels y texto explicativo |
| **Borde Estándar** | `var(--border-card)` | `#e2e8f0` | Líneas divisorias y bordes de tarjetas |

### Estados y Alertas (SUNAT, SLA, Despachos)
- **Éxito / Aprobado**: `var(--state-success)` (`#16a34a`) con fondo `var(--state-success-bg)` (`#dcfce7`).
- **Pendiente / Advertencia**: `var(--state-warning)` (`#f59e0b`) con fondo `var(--state-warning-bg)` (`#fef3c7`).
- **Retraso SLA / Peligro**: `var(--state-danger)` (`#ef4444`) con fondo `var(--state-danger-bg)` (`#fee2e2`).
- **Informativo**: `var(--state-info)` (`#0284c7`) con fondo `var(--state-info-bg)` (`#e0f2fe`).

---

## 2. 📦 Componentes Reutilizables (Copy-Paste)

### A. Tarjeta Estándar (`.prosegin-card`)
Toda sección, formulario o listado debe ir dentro de una tarjeta blanca con bordes sutiles:

```html
<div class="prosegin-card mb-4">
    <div class="prosegin-card-header">
        <h5 class="prosegin-card-title">
            <i class="bi bi-box-seam text-warning"></i> Título de la Sección
        </h5>
        <span class="badge-prosegin badge-prosegin-success">Activo</span>
    </div>
    <div class="prosegin-card-body">
        <!-- Contenido, tablas o campos aquí -->
    </div>
</div>
```

---

### B. Botones de Acción

```html
<!-- Botón Principal (Naranja corporativo para Crear, Guardar, Confirmar) -->
<button type="submit" class="btn-prosegin-primary">
    <i class="bi bi-check2-circle"></i> Guardar Cambios
</button>

<!-- Botón Secundario (Blanco con borde para Cancelar, Volver, Filtros) -->
<a href="#" class="btn-prosegin-secondary">
    <i class="bi bi-arrow-left"></i> Regresar
</a>

<!-- Botón Oscuro (Para acciones secundarias o de gestión) -->
<button class="btn-prosegin-dark">
    <i class="bi bi-printer"></i> Imprimir Rótulo
</button>
```

---

### C. Campos de Formulario (Inputs Limpios)

```html
<div class="mb-3">
    <label class="form-label-prosegin">Número de RUC</label>
    <div class="input-group">
        <span class="input-group-text bg-light border-end-0 text-muted">
            <i class="bi bi-hash"></i>
        </span>
        <input type="text" class="form-control form-control-prosegin border-start-0" placeholder="Ej: 20123456789" />
    </div>
    <div class="form-text text-muted small">Debe contener 11 dígitos numéricos.</div>
</div>
```

---

### D. Badges / Pastillas de Estado (Según Screenshot)

```html
<span class="badge-prosegin badge-prosegin-success">
    <i class="bi bi-check-circle"></i> GRE Emitida
</span>

<span class="badge-prosegin badge-prosegin-warning">
    <i class="bi bi-clock"></i> Listo Despacho
</span>

<span class="badge-prosegin badge-prosegin-danger">
    <i class="bi bi-exclamation-triangle"></i> Retraso SLA 42h
</span>

<span class="badge-prosegin badge-prosegin-neutral">
    <i class="bi bi-circle"></i> En Espera
</span>
```

---

### E. Tablas de Datos (`.table-prosegin`)

```html
<div class="table-responsive">
    <table class="table-prosegin">
        <thead>
            <tr>
                <th>Código Pedido</th>
                <th>Cliente</th>
                <th>Estado</th>
                <th>Monto Total</th>
                <th>Acciones</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td class="fw-bold">PED-2026-0842</td>
                <td>Volcan Compañía Minera S.A.A.</td>
                <td><span class="badge-prosegin badge-prosegin-warning">Listo Despacho</span></td>
                <td class="fw-bold">S/ 32,450.00</td>
                <td>
                    <button class="btn btn-sm btn-outline-secondary"><i class="bi bi-eye"></i></button>
                </td>
            </tr>
        </tbody>
    </table>
</div>
```

---

## 3. 🧭 Estructura del Menú Lateral (Sidebar)

Los estilos del menú lateral y layout se encuentran desacoplados en `wwwroot/css/sidebar.css`, y su marcado HTML está centralizado en `Views/Shared/_Layout.cshtml`.

Cuenta con los módulos oficiales:
1. `Catálogo de EPP`
2. `Búsqueda de Clientes`
3. `Edición de Precios`
4. `Gestión de Pedidos`
5. `Datos Tributarios` (actualmente en `/Clientes/Create`)

