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
    const trigger = document.getElementById('btnDropdownProveedores');
    const cerrar = document.getElementById('btnCerrarProveedores');
    const chevron = document.getElementById('dropdownChevron');
    const dropdownWrapper = document.getElementById('proveedoresDropdownWrapper');
    const checks = [...document.querySelectorAll('.proveedor-check')];
    const tarifaRows = [...document.querySelectorAll('[data-proveedor-tarifa-row]')];
    const principalRadios = [...document.querySelectorAll('.proveedor-principal')];
    const etiquetas = document.getElementById('proveedoresSeleccionados');
    const proveedorError = document.getElementById('proveedoresError');
    const archivo = document.getElementById('txtFichaTecnica');
    const zona = document.getElementById('dropzoneBox');
    const cargado = document.getElementById('boxFichaSeleccionada');
    const fichaError = document.getElementById('fichaError');
    const errorFormato = 'Solo se admiten archivos en formato PDF';
    const errorTamano = 'El archivo no debe superar los 5 MB';
    let skuTimer;
    let skuVersion = 0;
    let skuDuplicado = false;
    let skuVerificado = false;
    let skuPendiente = false;
    let fichaVersion = 0;
    let fichaValidada = false;
    let fichaObjectUrl = null;
    let fichaPagina = 1;
    let fichaTotalPaginas = 1;
    let fichaZoom = 100;
    let fichaViewer = null;

    const visor = document.getElementById('visorFichaTecnica');
    const visorCanvas = document.getElementById('visorFichaCanvas');
    const visorPaginaTexto = document.getElementById('visorPaginaTexto');
    const visorZoomTexto = document.getElementById('visorZoomTexto');

    function actualizarVisor() {
        if (!visorCanvas) return;
        visorPaginaTexto.textContent = `Página ${fichaPagina} de ${fichaTotalPaginas}`;
        visorZoomTexto.textContent = `${fichaZoom}%`;
    }

    async function mostrarVisor(file) {
        if (!visor || !visorCanvas) return;
        if (fichaObjectUrl) URL.revokeObjectURL(fichaObjectUrl);
        fichaObjectUrl = URL.createObjectURL(file);
        fichaPagina = 1;
        fichaTotalPaginas = await contarPaginasPdf(file);
        fichaZoom = 100;
        document.getElementById('visorFichaNombre').textContent = file.name;
        visor.classList.remove('d-none');
        fichaViewer = await window.ProseginPdfViewer.mount({
            canvas: visorCanvas,
            source: fichaObjectUrl,
            onChange: (pagina, total, zoom) => {
                fichaPagina = pagina;
                fichaTotalPaginas = total;
                fichaZoom = zoom;
                actualizarVisor();
            }
        });
    }

    async function contarPaginasPdf(file) {
        try {
            const bytes = new TextDecoder('latin1').decode(await file.arrayBuffer());
            const paginas = bytes.match(/\/Type\s*\/Page\b/g);
            return Math.max(1, paginas ? paginas.length : 1);
        } catch {
            return 1;
        }
    }

    function costoValido() {
        return costo.value !== '' && Number(costo.value) > 0 && costo.validity.valid;
    }

    function actualizarTarifasProveedores() {
        tarifaRows.forEach(row => {
            const providerId = row.dataset.proveedorTarifaRow;
            const seleccionado = checks.some(check => check.checked && check.value === providerId);
            const precio = row.querySelector('.proveedor-tarifa-costo');
            const plazo = row.querySelector('.proveedor-tarifa-plazo');
            const principal = row.querySelector('.proveedor-principal');
            precio.disabled = !seleccionado;
            plazo.disabled = !seleccionado;
            principal.disabled = !seleccionado;
            precio.required = seleccionado;
            plazo.required = seleccionado;
            const estado = row.querySelector('.badge, .text-muted');
            if (estado) {
                estado.textContent = seleccionado ? 'Autorizado' : 'No seleccionado';
                estado.classList.toggle('badge', seleccionado);
                estado.classList.toggle('text-bg-light', seleccionado);
                estado.classList.toggle('border', seleccionado);
                estado.classList.toggle('text-muted', !seleccionado);
            }
            if (seleccionado && precio.dataset.custom !== 'true'
                && Number(precio.value) <= 0 && Number(costo.value) > 0) {
                precio.value = costo.value;
            }
        });

        const seleccionados = checks.filter(check => check.checked).map(check => check.value);
        const principalActivo = principalRadios.some(radio => radio.checked && seleccionados.includes(radio.value));
        if (!principalActivo && seleccionados.length) {
            const sugerido = principalRadios.find(radio => radio.value === seleccionados[0]);
            if (sugerido) sugerido.checked = true;
        }
    }

    function actualizarEstado() {
        actualizarTarifasProveedores();
        const proveedoresActivos = checks.filter(c => c.checked).map(c => c.value);
        const principalValido = principalRadios.some(radio => radio.checked && proveedoresActivos.includes(radio.value));
        const tarifasValidas = tarifaRows.every(row => {
            if (!proveedoresActivos.includes(row.dataset.proveedorTarifaRow)) return true;
            const precio = row.querySelector('.proveedor-tarifa-costo');
            const plazo = row.querySelector('.proveedor-tarifa-plazo');
            return Number(precio.value) > 0 && precio.validity.valid && plazo.checkValidity();
        });
        const completo = sku.value.trim() && skuVerificado && nombre.value.trim()
            && categoria.value && costoValido() && proveedoresActivos.length > 0
            && principalValido && tarifasValidas && fichaValidada;
        const statusIconEl = document.getElementById('statusIcon');
        if (statusIconEl) {
            statusIconEl.className = 'material-symbols-outlined fs-5 ' + (completo ? 'text-success' : 'text-muted');
            statusIconEl.textContent = completo ? 'check_circle' : 'radio_button_unchecked';
        }
        const label = document.getElementById('statusLabel');
        label.className = completo ? 'small text-success fw-bold' : 'small text-muted fw-medium';
        label.textContent = completo ? 'Todos los campos obligatorios (*) completados correctamente'
            : 'Complete todos los campos obligatorios (*) para guardar';
    }

    function abrirProveedores(abierto) {
        if (!lista) return;
        lista.style.display = abierto ? 'block' : 'none';
        if (trigger) trigger.setAttribute('aria-expanded', String(abierto));
        if (chevron) chevron.style.transform = abierto ? 'rotate(180deg)' : 'rotate(0deg)';
        if (abierto) {
            filtrarProveedores();
            setTimeout(() => busqueda && busqueda.focus(), 60);
        }
    }

    function filtrarProveedores() {
        if (!busqueda) return;
        const termino = busqueda.value.trim().toLocaleLowerCase('es');
        let visibles = 0;
        checks.forEach(check => {
            const opcion = check.closest('.proveedor-opcion');
            if (!opcion) return;
            const coincide = (opcion.dataset.busqueda || '').toLocaleLowerCase('es').includes(termino);
            opcion.classList.toggle('d-none', !coincide);
            opcion.classList.toggle('d-flex', coincide);
            if (coincide) visibles++;
        });
        const sinResultados = document.getElementById('proveedoresSinResultados');
        if (sinResultados) sinResultados.classList.toggle('d-none', visibles > 0);
    }

    function actualizarProveedores() {
        if (!etiquetas) return;
        etiquetas.replaceChildren();
        const seleccionados = checks.filter(c => c.checked);

        // Estilos de selección en la lista
        checks.forEach(check => {
            const opcion = check.closest('.proveedor-opcion');
            if (opcion) {
                opcion.style.backgroundColor = check.checked ? '#f1f5f9' : '';
            }
        });

        // Crear chips con botón de quitar
        seleccionados.forEach(check => {
            const chip = document.createElement('span');
            chip.className = 'badge rounded-pill bg-light text-dark border d-inline-flex align-items-center gap-2 py-1 px-3 shadow-sm';
            chip.style.fontSize = '0.78rem';
            chip.style.fontWeight = '500';

            const icon = document.createElement('i');
            icon.className = 'material-symbols-outlined text-secondary';
            icon.style.fontSize = '1rem';
            icon.textContent = 'domain';
            chip.append(icon);

            const texto = document.createElement('span');
            texto.textContent = check.dataset.nombre || '';
            chip.append(texto);

            const quitar = document.createElement('button');
            quitar.type = 'button';
            quitar.className = 'btn-close ms-1';
            quitar.style.fontSize = '0.55rem';
            quitar.setAttribute('aria-label', `Quitar ${check.dataset.nombre}`);
            quitar.addEventListener('click', (e) => {
                e.stopPropagation();
                check.checked = false;
                actualizarProveedores();
            });
            chip.append(quitar);
            etiquetas.append(chip);
        });

        const countText = `${seleccionados.length} seleccionados`;
        const contadorTop = document.getElementById('proveedoresContador');
        if (contadorTop) contadorTop.textContent = countText;
        const contadorFooter = document.getElementById('proveedoresFooterCount');
        if (contadorFooter) contadorFooter.textContent = countText;

        const placeholder = document.getElementById('dropdownPlaceholder');
        const badgeCount = document.getElementById('proveedoresBadgeCount');
        if (placeholder) {
            if (seleccionados.length === 0) {
                placeholder.textContent = 'Seleccionar proveedores autorizados...';
                placeholder.className = 'text-muted';
                if (badgeCount) badgeCount.classList.add('d-none');
            } else {
                placeholder.textContent = seleccionados.length === 1
                    ? seleccionados[0].dataset.nombre
                    : `${seleccionados.length} proveedores seleccionados`;
                placeholder.className = 'text-dark fw-semibold';
                if (badgeCount) {
                    badgeCount.textContent = String(seleccionados.length);
                    badgeCount.classList.remove('d-none');
                }
            }
        }

        if (seleccionados.length && proveedorError) proveedorError.textContent = '';
        actualizarEstado();
    }

    if (trigger) {
        trigger.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            const isOpen = lista && lista.style.display === 'block';
            abrirProveedores(!isOpen);
        });
    }

    if (cerrar) {
        cerrar.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            abrirProveedores(false);
        });
    }

    if (busqueda) {
        busqueda.addEventListener('input', filtrarProveedores);
        busqueda.addEventListener('click', (e) => e.stopPropagation());
        busqueda.addEventListener('keydown', e => {
            if (e.key === 'Escape') abrirProveedores(false);
            if (e.key === 'Enter') e.preventDefault();
        });
    }

    document.querySelectorAll('.proveedor-opcion').forEach(opcion => {
        opcion.addEventListener('click', (e) => {
            if (e.target.tagName === 'INPUT') return;
            const check = opcion.querySelector('.proveedor-check');
            if (check) {
                check.checked = !check.checked;
                actualizarProveedores();
            }
        });
        opcion.addEventListener('mouseenter', () => {
            const check = opcion.querySelector('.proveedor-check');
            if (!check || !check.checked) {
                opcion.style.backgroundColor = '#f8fafc';
            }
        });
        opcion.addEventListener('mouseleave', () => {
            const check = opcion.querySelector('.proveedor-check');
            if (!check || !check.checked) {
                opcion.style.backgroundColor = '';
            }
        });
    });

    document.addEventListener('click', e => {
        if (dropdownWrapper && !dropdownWrapper.contains(e.target)) {
            abrirProveedores(false);
        }
    });
    checks.forEach(check => check.addEventListener('change', actualizarProveedores));
    tarifaRows.forEach(row => {
        const precio = row.querySelector('.proveedor-tarifa-costo');
        precio.addEventListener('input', () => {
            precio.dataset.custom = 'true';
            actualizarEstado();
        });
        row.querySelector('.proveedor-tarifa-plazo').addEventListener('input', actualizarEstado);
    });
    principalRadios.forEach(radio => radio.addEventListener('change', actualizarEstado));

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
        fichaTotalPaginas = 1;
        archivo.value = '';
        if (fichaObjectUrl) {
            URL.revokeObjectURL(fichaObjectUrl);
            fichaObjectUrl = null;
        }
        cargado.classList.add('d-none');
        zona.classList.remove('d-none');
        visor.classList.add('d-none');
        fichaViewer?.destroy();
        fichaViewer = null;
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
        let mensajeError = errorFormato;
        try {
            if (!file || !/\.pdf$/i.test(file.name)) {
                mensajeError = errorFormato;
            } else if (file.size > 5 * 1024 * 1024) {
                mensajeError = errorTamano;
            } else if (file.size >= 8) {
                const decoder = new TextDecoder('ascii');
                const cabecera = decoder.decode(await file.slice(0, 8).arrayBuffer());
                const final = decoder.decode(await file.slice(Math.max(0, file.size - 1024)).arrayBuffer());
                valida = /^%PDF-(1\.[0-7]|2\.0)$/.test(cabecera) && final.trimEnd().endsWith('%%EOF');
                if (!valida) mensajeError = errorFormato;
            }
        } catch {
            valida = false;
            mensajeError = errorFormato;
        }
        if (version !== fichaVersion) return;
        if (!valida) {
            resetearFicha();
            fichaError.textContent = mensajeError;
            window.ProseginToast?.error(mensajeError);
            return;
        }
        fichaValidada = true;
        document.getElementById('lblFichaNombre').textContent = file.name;
        document.getElementById('lblFichaTamano').textContent = `(${(file.size / 1024 / 1024).toFixed(2)} MB)`;
        cargado.classList.remove('d-none');
        zona.classList.add('d-none');
        await mostrarVisor(file);
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
        if (e.dataTransfer.files.length > 1) {
            window.ProseginToast?.error('Solo se admite un archivo de ficha técnica por producto');
            return;
        }
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
    document.getElementById('btnCambiarFicha').addEventListener('click', () => archivo.click());
    document.getElementById('visorPaginaAnterior').addEventListener('click', () => {
        fichaViewer?.previous().then(changed => {
            if (!changed) window.ProseginToast?.info('Ya se encuentra en la primera página del documento');
        });
    });
    document.getElementById('visorPaginaSiguiente').addEventListener('click', () => {
        fichaViewer?.next().then(changed => {
            if (!changed) window.ProseginToast?.info('Ya se encuentra en la última página del documento');
        });
    });
    document.getElementById('visorZoomMenos').addEventListener('click', () => {
        fichaViewer?.zoomOut();
    });
    document.getElementById('visorZoomMas').addEventListener('click', () => {
        fichaViewer?.zoomIn();
    });
    [nombre, categoria, costo].forEach(control => {
        control.addEventListener('input', actualizarEstado);
        control.addEventListener('change', actualizarEstado);
    });
    costo.addEventListener('input', () => {
        tarifaRows.forEach(row => {
            const precio = row.querySelector('.proveedor-tarifa-costo');
            const check = checks.find(option => option.value === row.dataset.proveedorTarifaRow);
            if (check && check.checked && precio.dataset.custom !== 'true') {
                precio.value = costo.value;
            }
        });
        actualizarEstado();
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
            if (!sku.value.trim() || !nombre.value.trim() || !categoria.value || !costo.value.trim()) {
                window.ProseginToast?.error('Complete los campos obligatorios');
            } else if (skuDuplicado) {
                window.ProseginToast?.error('El código SKU ya se encuentra registrado');
            } else if (!costoValido()) {
                window.ProseginToast?.error('Ingrese un costo base válido');
            } else {
                window.ProseginToast?.error('Por favor completa o corrige los campos obligatorios marcados en rojo.');
            }
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
        abrirProveedores(false);
    });

    actualizarProveedores();
    if (sku.value.trim()) {
        skuPendiente = true;
        validarSku(++skuVersion);
    }
});
