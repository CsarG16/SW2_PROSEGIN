# SYSTEM CONTEXT: PROSEGIN (SISTEMA DE COMPRA Y VENTA DE EPP)

Documentación central de contexto de negocio, arquitectura y requerimientos para el desarrollo en **Antigravity**.

---

## 1. IDENTIDAD, HISTORIA Y CONTEXTO DEL NEGOCIO
- **Empresa:** PROSEGIN S.A.C. (fundada a fines de diciembre de 2023).
- **Ubicación Física:** Conglomerado comercial de Las Malvinas (C.C. Boulevard Ferretero, Av. Guillermo Dansey, Lima, Perú).
- **Giro del Negocio:** Suministro corporativo de Equipos de Protección Personal (EPP): cascos de seguridad, calzado con punta de acero, chalecos reflectivos, guantes de maniobra, respiradores y normativos industriales.
- **Modelo Operativo:** Intermediación rápida sin inventario masivo fijo ("just in time" bajo pedido). No se alquilan grandes almacenes ni se inmoviliza capital en stock masivo.
- **Dinámica Comercial:**
  1. Al recibir requerimiento, los asesores consultan en tiempo real los puestos y almacenes de mayoristas aliados en Las Malvinas para obtener el mejor costo disponible.
  2. Aprobada la propuesta por el cliente, PROSEGIN compra la mercadería al contado en efectivo.
  3. Traslada los productos a su módulo para inspección de calidad y empaque consolidado (multi-fabricante).
  4. Despacha a la sede del cliente con su documentación legal (Guía de Remisión y Factura).
- **Tensión Financiera Crítica:**
  - Compras a mayoristas: 100% al contado / efectivo en el acto.
  - Ventas a clientes corporativos: Exigencia de crédito a 7, 15 o 30 días.
  - **Consecuencia:** Margen cero para errores en cálculo de precio de venta, demoras en cotizaciones o descuidos en cobranzas.

---

## 2. EL VIAJE OPERATIVO: AS-IS VS TO-BE

### Proceso AS-IS (Manual y con Fricciones):
1. **Recepción:** Requerimientos informales por WhatsApp.
2. **Consulta:** Mensajes dispersos a múltiples contactos mayoristas preguntando disponibilidad y costo.
3. **Cálculo en Excel:** Digitado manual de costos y fórmulas de margen, propenso a errores humanos de cálculo.
4. **Fichas Técnicas:** Búsqueda manual carpeta por carpeta en Google Drive de los PDFs de fichas técnicas obligatorias por ley para cada EPP cotizado; descarga y adjunto uno por uno. Genera demoras de horas o días (riesgo de pérdida del cliente frente a competidores).
5. **Cierre y Despacho:** Compra física y consolidación.
6. **Cuello de Botella Tributario:** Ingreso a SUNAT para transcribir manualmente datos para la Guía de Remisión Electrónica (GRE), y posterior retranscripción de los mismos datos para la Factura Electrónica a crédito.
7. **Cobranzas:** Cuentas por cobrar dispersas en cuadernos/carpetas sin calendario centralizado ni alertas de morosidad.

### Proceso TO-BE (Digitalizado con Prosegin Core):
1. **Atención Instantánea:** Selección de EPP desde catálogo digital centralizado e ingreso del costo de compra obtenido en Las Malvinas.
2. **Cálculo y Generación Documental Automatizada:** Aplicación inmediata de márgenes de rentabilidad, extracción automática de fichas técnicas desde repositorio interno y compilación en 1 solo clic de un PDF unificado (propuesta económica + fichas técnicas) en < 12 minutos.
3. **Conversión a Orden y Despacho Rápido:** Conversión inmediata de cotización aprobada a Orden de Venta. Al marcar "Listo para despacho", emisión automática de GRE y Factura Electrónica sin doble digitación.
4. **Protección Financiera Activa:** Control de vencimientos de crédito (7, 15, 30 días) en tablero Kanban con alertas activas y bloqueo automático de nuevas ventas a clientes morosos.

---

## 3. OBJETIVOS Y MÉTRICAS DE IMPACTO
- **Objetivo General:** Implementar una solución web transaccional integral para automatizar, centralizar y sincronizar el ciclo comercial completo (compras, ventas, facturación y cobranzas).
- **Métricas Clave:**
  - Reducir tiempo de emisión de cotizaciones a **< 12 minutos** (frente a horas/días del AS-IS).
  - Procesar centralizadamente **> 500 transacciones mensuales** con alta disponibilidad.
  - **Eliminar la doble digitación tributaria** mediante emisión automática diaria de Facturas SUNAT y GRE.
  - **Cero fuga de márgenes y reducción de morosidad** mediante alertas y bloqueos de crédito.

---

## 4. ALCANCE Y MÓDULOS FUNCIONALES

### Módulo 1: Administración de Clientes y Directorio Corporativo (Logística / Ventas)
- **MVP:**
  - Registro y actualización de datos tributarios corporativos (RUC y razón social validados).
  - Búsqueda ágil por RUC o Razón Social.
  - Registro de dirección fiscal y múltiples puntos de entrega física para despachos.
- **Release 2:**
  - Registro de contactos comerciales por cliente (nombre, cargo, correo, teléfono).
  - Historial comercial consolidado de cotizaciones y compras pasadas.

### Módulo 2: Catálogo de EPP y Repositorio de Fichas Técnicas (Logística / Catálogo)
- **MVP:**
  - Registro y clasificación por código SKU y categorías de protección (cabeza, pies, manos, cuerpo, respiratoria, auditiva, etc.).
  - Filtros de búsqueda rápida por código o categoría.
  - Subida y vinculación directa del archivo PDF de la ficha técnica oficial a cada producto en base de datos.
- **Release 2:**
  - Visualizador previo de PDF integrado sin salir del sistema.
  - Historial de proveedores locales vinculados al producto y costos base referenciales.

### Módulo 3: Motor de Cotizaciones y Órdenes de Venta (Área Comercial)
- **MVP:**
  - Selección interactiva de productos desde el catálogo centralizado.
  - Asignación de costo unitario de proveedor y cálculo automático de precio de venta:
    $$\text{Precio Venta} = \frac{\text{Costo Proveedor}}{1 - \text{Margen Deseado}}$$
  - Generación de propuesta formal en un único PDF consolidado con fichas técnicas anexadas.
  - Conversión inmediata de cotización aprobada a Orden de Venta.
- **Release 2:**
  - Histórico y trazabilidad de cotizaciones emitidas.
  - Estados logísticos del pedido en tiempo real: *En preparación*, *En consolidación*, *Despachado*, *Entregado*.

### Módulo 4: Abastecimiento y Compras a Proveedores (Finanzas / Compras)
- **MVP:**
  - Generación de órdenes de compra para proveedores mayoristas de Las Malvinas a partir de cotizaciones ganadas.
- **Release 2:**
  - Auditoría de variación del costo real cobrado vs. costo cotizado (control de fugas de margen).
  - Control de recepción física y verificación de calidad de la mercadería consolidada.

### Módulo 5: Facturación Electrónica y Control de Cobranzas (Finanzas / Gerencia)
- **MVP:**
  - Emisión automatizada de Factura Electrónica directa desde la venta/cotización.
  - Emisión de Guía de Remisión Electrónica Remitente (GRE) para el traslado de EPP.
  - Configuración de condiciones de crédito corporativo y plazos (7, 15 o 30 días).
- **Release 2:**
  - Tablero visual tipo Kanban para cuentas por cobrar, facturas emitidas y vencimientos.
  - Descarga y gestión de comprobantes (PDF y XML tributario).

---

## 5. LINEAMIENTOS Y REGLAS TÉCNICAS DEL PROYECTO

### Stack Tecnológico:
- **Backend / Arquitectura:** .NET 8 con estructura pragmática de 2 proyectos:
  - `Prosegin.Data`: Capa de datos y modelos. Contiene las entidades (`Entities/`), el `ProseginDbContext` con Entity Framework Core y las migraciones de MySQL.
  - `Prosegin.Web`: Aplicación web y lógica de negocio. Contiene los servicios de negocio (`Services/`), controladores MVC, vistas Razor, configuración e inyección de dependencias.
- **Base de Datos:** MySQL mediante ORM Entity Framework Core con el proveedor:
  - `Pomelo.EntityFrameworkCore.MySql` (versión 8.0.2).
- **Frontend:** Vistas Razor, Bootstrap 5 y JavaScript/AJAX para cálculos dinámicos de cotizaciones en pantalla.

### Reglas Críticas de Implementación:
1. **Lógica de Negocio en Servicios:** La lógica de cálculo de márgenes, validación de clientes morosos y manejo de PDFs se organiza en `Prosegin.Web/Services/`.
2. **Fórmula de Precio de Venta:** Debe validarse y calcularse siempre en el backend (servidor) antes de persistir:
   $$\text{Precio Venta} = \frac{\text{Costo Proveedor}}{1 - \text{Margen Deseado}}$$
3. **Almacenamiento de Fichas Técnicas:** Los archivos físicos PDF se guardan en el servidor (ej. `wwwroot/uploads/fichas/` o servicio de almacenamiento). En MySQL solo se almacena la ruta relativa, nombre original y metadata.
4. **Protección Contra Morosidad:** Al crear o aprobar una cotización a crédito, el sistema debe verificar de forma obligatoria si el cliente posee facturas impagas vencidas. Si existe deuda vencida, se debe restringir o requerir autorización gerencial.
