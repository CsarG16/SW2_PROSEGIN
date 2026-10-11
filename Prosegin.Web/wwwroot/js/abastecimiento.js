document.addEventListener('DOMContentLoaded', () => {
    const productos = window.abastecimientoProductos || [];
    const filas = [...document.querySelectorAll('[data-product-row]')];
    const resumen = document.getElementById('resumenProveedores');
    const totalConsolidado = document.getElementById('totalConsolidado');
    const cantidadOrdenes = document.getElementById('cantidadOrdenes');
    const botonGenerar = document.getElementById('generarOrdenes');
    const modalElement = document.getElementById('vistaPreviaOrdenCompra');
    const modal = window.bootstrap ? new bootstrap.Modal(modalElement) : null;
    const moneda = new Intl.NumberFormat('es-PE', {
        style: 'currency',
        currency: 'PEN',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });

    function obtenerOrdenesAgrupadas() {
        const grupos = new Map();
        filas.forEach(fila => {
            const indice = Number(fila.dataset.productIndex);
            const producto = productos[indice];
            const selector = fila.querySelector('.proveedor-abastecimiento');
            const opcion = selector && selector.selectedOptions[0];
            if (!producto || !opcion) return;

            const proveedorId = Number(opcion.value);
            const costoUnitario = Number(opcion.dataset.unitPrice);
            const subtotal = Math.round((producto.Cantidad * costoUnitario + Number.EPSILON) * 100) / 100;
            let grupo = grupos.get(proveedorId);
            if (!grupo) {
                grupo = {
                    id: proveedorId,
                    nombre: opcion.dataset.providerName,
                    lineas: [],
                    total: 0
                };
                grupos.set(proveedorId, grupo);
            }
            grupo.lineas.push({ producto, costoUnitario, subtotal });
            grupo.total = Math.round((grupo.total + subtotal + Number.EPSILON) * 100) / 100;
            const subtotalCelda = fila.querySelector('[data-product-subtotal]');
            if (subtotalCelda) subtotalCelda.textContent = moneda.format(subtotal);
        });
        return [...grupos.values()].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
    }

    function actualizarResumen() {
        const ordenes = obtenerOrdenesAgrupadas();
        resumen.replaceChildren();
        let total = 0;

        ordenes.forEach(orden => {
            total = Math.round((total + orden.total + Number.EPSILON) * 100) / 100;
            const tarjeta = document.createElement('div');
            tarjeta.className = 'border rounded-3 p-3 d-flex flex-wrap align-items-center justify-content-between gap-2 bg-white';

            const proveedor = document.createElement('div');
            proveedor.className = 'me-auto';
            const nombre = document.createElement('strong');
            nombre.className = 'd-block';
            nombre.textContent = orden.nombre;
            const cantidad = document.createElement('span');
            cantidad.className = 'small text-muted';
            cantidad.textContent = `${orden.lineas.length} ${orden.lineas.length === 1 ? 'ítem' : 'ítems'}`;
            proveedor.append(nombre, cantidad);

            const subtotal = document.createElement('strong');
            subtotal.className = 'font-monospace';
            subtotal.textContent = moneda.format(orden.total);

            const ver = document.createElement('button');
            ver.type = 'button';
            ver.className = 'btn btn-sm btn-light border';
            ver.dataset.previewProvider = String(orden.id);
            ver.textContent = 'Ver OC';
            ver.addEventListener('click', () => mostrarVistaPrevia(orden.id));

            tarjeta.append(proveedor, subtotal, ver);
            resumen.append(tarjeta);
        });

        totalConsolidado.textContent = moneda.format(total);
        cantidadOrdenes.textContent = `${ordenes.length} ${ordenes.length === 1 ? 'orden agrupada' : 'órdenes agrupadas'}`;
        if (ordenes.length && filas.every(fila => fila.querySelector('.proveedor-abastecimiento'))) {
            botonGenerar.disabled = false;
        }
    }

    function agregarCelda(fila, texto, clase, alineadaDerecha = false) {
        const celda = document.createElement('td');
        celda.textContent = texto;
        if (clase) celda.className = clase;
        if (alineadaDerecha) celda.classList.add('text-end');
        fila.append(celda);
    }

    function mostrarVistaPrevia(proveedorId) {
        const orden = obtenerOrdenesAgrupadas().find(item => item.id === proveedorId);
        if (!orden || !modal) return;

        document.getElementById('vistaPreviaTitulo').textContent = `Orden de compra · ${orden.nombre}`;
        const cuerpo = document.getElementById('vistaPreviaLineas');
        cuerpo.replaceChildren();
        orden.lineas.forEach(linea => {
            const fila = document.createElement('tr');
            agregarCelda(fila, linea.producto.Nombre);
            agregarCelda(fila, `${linea.producto.Cantidad} ${linea.producto.UnidadMedida}`, '', true);
            agregarCelda(fila, moneda.format(linea.costoUnitario), 'font-monospace', true);
            agregarCelda(fila, moneda.format(linea.subtotal), 'font-monospace', true);
            cuerpo.append(fila);
        });
        document.getElementById('vistaPreviaTotal').textContent = moneda.format(orden.total);
        modal.show();
    }

    filas.forEach(fila => {
        const selector = fila.querySelector('.proveedor-abastecimiento');
        if (selector) selector.addEventListener('change', actualizarResumen);
    });

    actualizarResumen();
});
