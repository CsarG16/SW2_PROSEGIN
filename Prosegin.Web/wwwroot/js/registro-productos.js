document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('formRegistroProducto');
    const sku = document.getElementById('txtSku');
    const nombre = document.getElementById('txtNombre');
    const categoria = document.getElementById('ddlCategoria');
    const costo = document.getElementById('txtCosto');
    const guardar = document.getElementById('btnGuardarProducto');
    const skuIcon = document.getElementById('skuCheckIcon');
    const skuMensaje = document.getElementById('skuHelperText');
    const busqueda = document.getElementById('txtProveedorBusqueda');
    const lista = document.getElementById('proveedoresList');
    const agregar = document.getElementById('btnAgregarProveedor');
    const checks = [...document.querySelectorAll('.proveedor-check')];
    const etiquetas = document.getElementById('proveedoresSeleccionados');
    const proveedorError = document.getElementById('proveedoresError');
    const archivo = document.getElementById('txtFichaTecnica');
    const zona = document.getElementById('dropzoneBox');
    const cargado = document.getElementById('boxFichaSeleccionada');
    const fichaError = document.getElementById('fichaError');
    const errorArchivo = 'El archivo no cumple con el formato o tamaño admitido';
    let skuTimer;
    let skuVersion = 0;
    let skuDuplicado = false;
    let skuVerificado = false;
    let skuPendiente = false;
    let fichaVersion = 0;
    let fichaValidada = false;

    function costoValido() {
        return costo.value !== '' && Number(costo.value) > 0 && costo.validity.valid;
    }

    function actualizarEstado() {
        const completo = sku.value.trim() && skuVerificado && nombre.value.trim()
            && categoria.value && costoValido() && checks.some(c => c.checked) && fichaValidada;
        document.getElementById('statusIcon').className = completo
            ? 'bi bi-check-circle-fill text-success fs-5' : 'bi bi-check-circle text-muted fs-5';
        const label = document.getElementById('statusLabel');
        label.className = completo ? 'small text-success fw-bold' : 'small text-muted fw-medium';
        label.textContent = completo ? 'Todos los campos obligatorios (*) completados correctamente'
            : 'Complete todos los campos obligatorios (*) para guardar';
    }

    function filtrarProveedores() {
        const termino = busqueda.value.trim().toLocaleLowerCase('es');
        let visibles = 0;
        checks.forEach(check => {
            const opcion = check.closest('.proveedor-opcion');
            const coincide = opcion.dataset.busqueda.toLocaleLowerCase('es').includes(termino);
            // Bootstrap d-flex tiene prioridad sobre el atributo hidden.
            opcion.classList.toggle('d-none', !coincide);
            opcion.classList.toggle('d-flex', coincide);
            if (coincide) visibles++;
        });
        document.getElementById('proveedoresSinResultados').hidden = visibles > 0;
    }

    function abrirProveedores(abierto) {
        lista.hidden = !abierto;
        agregar.setAttribute('aria-expanded', String(abierto));
        if (abierto) filtrarProveedores();
    }

    function actualizarProveedores() {
        etiquetas.replaceChildren();
        const seleccionados = checks.filter(c => c.checked);
        seleccionados.forEach(check => {
            const etiqueta = document.createElement('span');
            etiqueta.className = 'badge rounded-pill bg-success-subtle text-success border d-inline-flex align-items-center gap-2';
            etiqueta.append(document.createTextNode(check.dataset.nombre));
            const quitar = document.createElement('button');
            quitar.type = 'button';
            quitar.className = 'btn-close';
            quitar.setAttribute('aria-label', `Quitar ${check.dataset.nombre}`);
            quitar.addEventListener('click', () => {
                check.checked = false;
                actualizarProveedores();
            });
            etiqueta.append(quitar);
            etiquetas.append(etiqueta);
        });
        document.getElementById('proveedoresContador').textContent = `${seleccionados.length} seleccionados`;
        if (seleccionados.length) proveedorError.textContent = '';
        actualizarEstado();
    }

    agregar.addEventListener('click', () => {
        abrirProveedores(lista.hidden);
        if (!lista.hidden) busqueda.focus();
    });
    busqueda.addEventListener('focus', () => abrirProveedores(true));
    busqueda.addEventListener('input', filtrarProveedores);
    busqueda.addEventListener('keydown', e => {
        if (e.key === 'Escape') abrirProveedores(false);
        if (e.key === 'Enter') e.preventDefault();
    });
    document.addEventListener('click', e => {
        if (!lista.contains(e.target) && e.target !== busqueda && !agregar.contains(e.target)) {
            abrirProveedores(false);
        }
    });
    checks.forEach(check => check.addEventListener('change', actualizarProveedores));

    async function validarSku(version) {
        const valor = sku.value.trim();
        if (!valor) return;
        try {
            const url = new URL('ValidarSku', window.location.href);
            url.searchParams.set('sku', valor);
            const response = await fetch(url);
            if (!response.ok) throw new Error('No se pudo verificar el SKU');
            const data = await response.json();
            if (version !== skuVersion) return;
            skuDuplicado = data.existe === true;
            skuVerificado = data.valido === true;
            sku.classList.toggle('is-invalid', !skuVerificado);
            sku.classList.toggle('is-valid', skuVerificado);
            skuIcon.classList.toggle('d-none', !skuVerificado);
            skuMensaje.className = skuVerificado ? 'mt-1 small text-success' : 'mt-1 small text-danger';
            skuMensaje.textContent = data.mensaje;
        } catch {
            if (version !== skuVersion) return;
            skuMensaje.className = 'mt-1 small text-muted';
            skuMensaje.textContent = 'El código SKU se comprobará al guardar.';
        } finally {
            if (version === skuVersion) {
                skuPendiente = false;
                actualizarEstado();
            }
        }
    }

    sku.addEventListener('input', () => {
        sku.value = sku.value.toUpperCase();
        clearTimeout(skuTimer);
        const version = ++skuVersion;
        skuDuplicado = false;
        skuVerificado = false;
        skuPendiente = !!sku.value.trim();
        sku.classList.remove('is-valid', 'is-invalid');
        skuIcon.classList.add('d-none');
        skuMensaje.className = 'mt-1 small text-muted';
        skuMensaje.textContent = skuPendiente ? 'Comprobando código SKU...' : 'Ingrese un código SKU único.';
        if (skuPendiente) skuTimer = setTimeout(() => validarSku(version), 350);
        actualizarEstado();
    });

    function resetearFicha() {
        fichaVersion++;
        fichaValidada = false;
        archivo.value = '';
        cargado.classList.add('d-none');
        zona.classList.remove('d-none');
        document.getElementById('lblFichaNombre').textContent = '';
        document.getElementById('lblFichaTamano').textContent = '';
        fichaError.textContent = '';
        actualizarEstado();
    }

    async function procesarFicha(file) {
        const version = ++fichaVersion;
        fichaValidada = false;
        cargado.classList.add('d-none');
        zona.classList.remove('d-none');
        fichaError.textContent = '';
        actualizarEstado();
        let valida = false;
        try {
            if (file && /\.pdf$/i.test(file.name) && file.size >= 8 && file.size <= 5 * 1024 * 1024) {
                const decoder = new TextDecoder('ascii');
                const cabecera = decoder.decode(await file.slice(0, 8).arrayBuffer());
                const final = decoder.decode(await file.slice(Math.max(0, file.size - 1024)).arrayBuffer());
                valida = /^%PDF-(1\.[0-7]|2\.0)$/.test(cabecera) && final.trimEnd().endsWith('%%EOF');
            }
        } catch {
            valida = false;
        }
        if (version !== fichaVersion) return;
        if (!valida) {
            resetearFicha();
            fichaError.textContent = errorArchivo;
            return;
        }
        fichaValidada = true;
        document.getElementById('lblFichaNombre').textContent = file.name;
        document.getElementById('lblFichaTamano').textContent = `(${(file.size / 1024 / 1024).toFixed(2)} MB)`;
        cargado.classList.remove('d-none');
        zona.classList.add('d-none');
        actualizarEstado();
    }

    zona.addEventListener('click', () => archivo.click());
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(event => {
        zona.addEventListener(event, e => {
            e.preventDefault();
            e.stopPropagation();
        });
    });
    zona.addEventListener('drop', e => {
        if (!e.dataTransfer.files.length) return;
        const transferencia = new DataTransfer();
        transferencia.items.add(e.dataTransfer.files[0]);
        archivo.files = transferencia.files;
        procesarFicha(archivo.files[0]);
    });
    archivo.addEventListener('change', () => {
        if (archivo.files.length) procesarFicha(archivo.files[0]);
        else resetearFicha();
    });
    document.getElementById('btnQuitarFicha').addEventListener('click', resetearFicha);
    [nombre, categoria, costo].forEach(control => {
        control.addEventListener('input', actualizarEstado);
        control.addEventListener('change', actualizarEstado);
    });

    form.addEventListener('submit', e => {
        let valido = form.checkValidity();
        [sku, nombre, categoria, costo].forEach(control => {
            const correcto = control.value.trim() !== '' && control.checkValidity();
            control.classList.toggle('is-invalid', !correcto);
            if (!correcto) valido = false;
        });
        if (skuDuplicado || skuPendiente) {
            sku.classList.add('is-invalid');
            valido = false;
        }
        if (!costoValido()) {
            costo.classList.add('is-invalid');
            valido = false;
        }
        if (!checks.some(c => c.checked)) {
            proveedorError.textContent = 'Seleccione al menos un proveedor autorizado.';
            abrirProveedores(true);
            valido = false;
        }
        if (!fichaValidada) {
            if (!fichaError.textContent) fichaError.textContent = 'Adjunte una ficha técnica PDF validada.';
            valido = false;
        }
        if (!valido || (window.jQuery && !window.jQuery(form).valid())) {
            e.preventDefault();
            return;
        }
        guardar.disabled = true;
        guardar.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span> Guardando producto...';
    });

    document.getElementById('btnCancelar').addEventListener('click', () => {
        clearTimeout(skuTimer);
        skuVersion++;
        form.reset();
        form.querySelectorAll('input:not([type="hidden"]), select').forEach(control => {
            if (control.type === 'checkbox') control.checked = false;
            else control.value = '';
        });
        resetearFicha();
        actualizarProveedores();
    });

    actualizarProveedores();
    if (sku.value.trim()) {
        skuPendiente = true;
        validarSku(++skuVersion);
    }
});
