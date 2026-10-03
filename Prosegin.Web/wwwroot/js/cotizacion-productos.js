(() => {
    const form = document.getElementById("formCotizacion");
    if (!form) return;

    const storageKey = `prosegin.quote.cart.v2.${form.dataset.clienteId}`;
    const pendingKey = "prosegin.quote.pending.v1";
    const rows = Array.from(document.querySelectorAll("[data-product-row]"));
    const selector = document.getElementById("selectorProducto");
    const search = document.getElementById("filtroProductos");
    const results = document.getElementById("resultadosBusqueda");
    const addButton = document.getElementById("agregarProducto");
    const quantityToAdd = document.getElementById("cantidadAgregar");
    const quantityError = document.getElementById("errorCantidadAgregar");
    const productsError = document.getElementById("productosError");
    const maxQuantity = 2147483647;
    const quantityMessage = "La cantidad debe ser un número entero mayor a cero y no exceder 2147483647.";
    const currency = new Intl.NumberFormat("es-PE", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const formatAmount = amount => `S/ ${currency.format(amount)}`;
    const selection = row => row.querySelector("input.quote-selection-input");
    const quantity = row => row.querySelector('input[name$=".Cantidad"]');
    const validQuantity = value => /^\d+$/.test(String(value).trim())
        && Number.isInteger(Number(value)) && Number(value) > 0 && Number(value) <= maxQuantity;

    // El almacenamiento puede estar deshabilitado; la cotización sigue funcionando.
    const readCart = (key = storageKey) => {
        try {
            const cart = JSON.parse(sessionStorage.getItem(key) || "[]");
            return Array.isArray(cart) ? cart : [];
        } catch { return []; }
    };
    const discardCart = () => {
        try { sessionStorage.removeItem(storageKey); } catch { /* Sin almacenamiento disponible. */ }
    };
    const writeCart = cart => {
        if (cart.length === 0) { discardCart(); return; }
        try { sessionStorage.setItem(storageKey, JSON.stringify(cart)); } catch { /* Sin almacenamiento disponible. */ }
    };

    // El borrador antiguo no identifica al cliente y no se puede migrar con seguridad.
    try { sessionStorage.removeItem("prosegin.quote.cart.v1"); } catch { /* Sin almacenamiento disponible. */ }

    const updateProductDisplay = row => {
        document.getElementById("productoSeleccionadoDesc").value = row
            ? `${row.querySelector("td:nth-child(2)").textContent.trim()} - ${row.querySelector(".quote-row-description").textContent.trim()}` : "";
        document.getElementById("unidadSeleccionada").value = row ? row.dataset.unit : "";
        document.getElementById("precioSeleccionado").value = row ? formatAmount(Number(row.dataset.cost)) : "";
        addButton.disabled = !row;
    };

    const resetEntry = () => {
        selector.value = "";
        search.value = "";
        quantityToAdd.value = "1";
        quantityToAdd.setCustomValidity("");
        quantityToAdd.classList.remove("is-invalid");
        quantityError.hidden = true;
        quantityError.textContent = "";
        results.replaceChildren();
        results.hidden = true;
        updateProductDisplay(null);
    };

    const updateTotals = () => {
        let subtotalCents = 0;
        let count = 0;
        let invalid = false;
        const cart = [];
        rows.forEach(row => {
            const selected = selection(row).checked;
            const input = quantity(row);
            input.disabled = !selected;
            row.classList.toggle("d-none", !selected);
            const valid = !selected || validQuantity(input.value);
            input.setCustomValidity(valid ? "" : quantityMessage);
            input.classList.toggle("is-invalid", !valid);
            if (!selected) return;
            row.querySelector("[data-row-number]").textContent = String(++count);
            if (!valid) {
                invalid = true;
                row.querySelector("[data-line-subtotal]").textContent = "—";
                return;
            }
            const units = Number(input.value);
            // Los costos del catálogo tienen dos decimales. Calcular en céntimos
            // evita que 30.25 * 0.18 se redondee a 5.44 por precisión binaria.
            const lineSubtotalCents = Math.round(Number(row.dataset.cost) * 100) * units;
            row.querySelector("[data-line-subtotal]").textContent = formatAmount(lineSubtotalCents / 100);
            subtotalCents += lineSubtotalCents;
            cart.push({ id: Number(row.dataset.productId), quantity: units });
        });
        const igvCents = Math.round(subtotalCents * 18 / 100);
        document.getElementById("subtotalCotizacion").textContent = invalid ? "—" : formatAmount(subtotalCents / 100);
        document.getElementById("igvCotizacion").textContent = invalid ? "—" : formatAmount(igvCents / 100);
        document.getElementById("totalCotizacion").textContent = invalid ? "—" : formatAmount((subtotalCents + igvCents) / 100);
        document.getElementById("cantidadProductos").textContent = String(count);
        document.getElementById("productosVacios").classList.toggle("d-none", count > 0);
        productsError.hidden = !invalid;
        productsError.textContent = invalid ? quantityMessage : "";
        // Nunca guardar cantidades corregidas silenciosamente ni una lista parcial.
        if (!invalid) writeCart(cart);
        return !invalid;
    };

    const clearProducts = () => {
        rows.forEach(row => {
            selection(row).checked = false;
            quantity(row).value = "1";
        });
        resetEntry();
        updateTotals();
        discardCart();
    };

    const restoreCart = (cart = readCart()) => {
        cart.forEach(item => {
            const row = rows.find(candidate => Number(candidate.dataset.productId) === Number(item?.id));
            if (!row || !validQuantity(item.quantity)) return;
            selection(row).checked = true;
            quantity(row).value = String(item.quantity);
        });
    };

    if (form.dataset.cotizacionGuardada === "true" || form.dataset.postInvalido === "true") {
        discardCart();
    } else if (!rows.some(row => selection(row).checked)) {
        const pending = readCart(pendingKey);
        if (pending.length > 0) {
            // "Continuar con cliente" inicia una nueva selección para el cliente elegido.
            restoreCart(pending);
            try { sessionStorage.removeItem(pendingKey); } catch { /* Sin almacenamiento disponible. */ }
        } else {
            restoreCart();
        }
    }

    const renderSearchResults = () => {
        const term = search.value.trim().toLocaleLowerCase();
        const matches = rows.filter(row => row.dataset.search.includes(term));
        // Editar la búsqueda invalida la selección anterior.
        selector.value = "";
        updateProductDisplay(null);
        results.replaceChildren();
        results.hidden = false;
        if (matches.length === 0) {
            const message = document.createElement("div");
            message.className = "list-group-item text-muted";
            message.textContent = "No se encontraron productos.";
            results.append(message);
        }
        matches.forEach(row => {
            const result = document.createElement("button");
            result.type = "button";
            result.className = "list-group-item list-group-item-action text-start d-flex align-items-center gap-3 py-2";
            const image = document.createElement("img");
            image.src = row.dataset.img || "";
            image.alt = "";
            image.width = 32;
            image.height = 32;
            image.style.objectFit = "contain";
            image.addEventListener("error", () => { image.hidden = true; });
            const label = document.createElement("span");
            label.textContent = `${row.querySelector("td:nth-child(2)").textContent.trim()} - ${row.querySelector(".quote-row-description").textContent.trim()}`;
            result.append(image, label);
            result.addEventListener("click", () => {
                selector.value = row.dataset.productId;
                search.value = row.querySelector(".quote-row-description").textContent.trim();
                results.hidden = true;
                updateProductDisplay(row);
            });
            results.append(result);
        });
    };

    search.addEventListener("input", renderSearchResults);
    search.addEventListener("focus", renderSearchResults);
    document.addEventListener("click", event => {
        if (!results.contains(event.target) && event.target !== search) results.hidden = true;
    });
    quantityToAdd.addEventListener("input", () => {
        quantityToAdd.setCustomValidity("");
        quantityToAdd.classList.remove("is-invalid");
        quantityError.hidden = true;
    });

    addButton.addEventListener("click", () => {
        const row = rows.find(candidate => candidate.dataset.productId === selector.value);
        if (!row) return;
        const existing = selection(row).checked ? Number(quantity(row).value) : 0;
        const totalQuantity = existing + Number(quantityToAdd.value);
        if (!validQuantity(quantityToAdd.value) || (selection(row).checked && !validQuantity(quantity(row).value))
            || !validQuantity(totalQuantity)) {
            quantityError.textContent = quantityMessage;
            quantityError.hidden = false;
            quantityToAdd.classList.add("is-invalid");
            quantityToAdd.setCustomValidity(quantityMessage);
            quantityToAdd.focus();
            return;
        }
        quantity(row).value = String(totalQuantity);
        selection(row).checked = true;
        updateTotals();
        resetEntry();
    });

    rows.forEach(row => {
        quantity(row).addEventListener("input", updateTotals);
        selection(row).addEventListener("change", updateTotals);
        row.querySelector("[data-remove-product]").addEventListener("click", () => {
            selection(row).checked = false;
            quantity(row).value = "1";
            updateTotals();
        });
    });
    document.getElementById("limpiarProductos").addEventListener("click", clearProducts);
    document.querySelectorAll("[data-cancel-quote]").forEach(link => link.addEventListener("click", clearProducts));
    form.addEventListener("submit", event => {
        const valid = updateTotals();
        if (!valid || !rows.some(row => selection(row).checked)) {
            event.preventDefault();
            productsError.textContent = valid ? "Selecciona al menos un producto del catálogo." : quantityMessage;
            productsError.hidden = false;
        }
    });
    // El navegador puede recuperar la página anterior desde su caché de navegación.
    window.addEventListener("pageshow", event => {
        if (!event.persisted) return;
        rows.forEach(row => { selection(row).checked = false; quantity(row).value = "1"; });
        resetEntry();
        restoreCart();
        updateTotals();
    });
    updateTotals();
})();
