const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');

// DOM mínimo para ejecutar el archivo de producción y comprobar sus eventos.
// No reemplaza la comprobación visual en un navegador.
const script = readFileSync(join(__dirname, '../../Prosegin.Web/wwwroot/js/cotizacion-productos.js'), 'utf8');
class Element {
    constructor() {
        this.value = ''; this.textContent = ''; this.checked = false;
        this.disabled = false; this.hidden = false; this.dataset = {};
        this.style = {}; this.listeners = {}; this.children = []; this.classes = new Set();
        this.classList = {
            add: name => this.classes.add(name), remove: name => this.classes.delete(name),
            contains: name => this.classes.has(name),
            toggle: (name, enabled) => enabled ? this.classes.add(name) : this.classes.delete(name)
        };
    }
    addEventListener(type, callback) { (this.listeners[type] ??= []).push(callback); }
    fire(type, extra = {}) {
        const event = { target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...extra };
        for (const callback of this.listeners[type] ?? []) callback(event);
        return event;
    }
    setCustomValidity(message) { this.validationMessage = message; }
    focus() { this.focused = true; }
    replaceChildren() { this.children = []; }
    append(...items) { for (const item of items) item.parent = this; this.children.push(...items); }
    setAttribute(name, value) { this[name] = value; }
    remove() { this.parent.children = this.parent.children.filter(child => child !== this); }
    contains(element) { return this.children.includes(element); }
}

function screen({ storage = new Map(), clientId = 1, saved = false, invalidPost = false, serverQuantity } = {}) {
    const ids = {};
    for (const id of ['formCotizacion', 'selectorProducto', 'filtroProductos', 'resultadosBusqueda',
        'agregarProducto', 'cantidadAgregar', 'errorCantidadAgregar', 'productosError',
        'productoSeleccionadoDesc', 'unidadSeleccionada', 'precioSeleccionado',
        'subtotalCotizacion', 'igvCotizacion', 'totalCotizacion', 'cantidadProductos',
        'productosVacios', 'limpiarProductos', 'margenEstimadoCotizacion', 'alertaPrecioError', 'alertaPrecioErrorTexto']) ids[id] = new Element();
    ids.formCotizacion.dataset = { clienteId: String(clientId), cotizacionGuardada: String(saved), postInvalido: String(invalidPost) };
    ids.cantidadAgregar.value = '1';
    const rows = [1, 2].map(id => {
        const row = new Element();
        row.dataset = { productId: String(id), cost: id === 1 ? '10' : '0.25', unit: 'UND', search: `sku${id} casco`, img: '' };
        const fields = {};
        for (const key of ['input.quote-selection-input', 'input[name$=".Cantidad"]', 'td:nth-child(2)',
            '.quote-row-description', '[data-row-number]', '[data-line-subtotal]', '[data-remove-product]',
            'input.quote-price-input', 'input.quote-margin-input', '.quote-margin-badge']) fields[key] = new Element();
        fields['input.quote-price-input'].value = row.dataset.cost;
        fields['input[name$=".Cantidad"]'].value = id === 1 && serverQuantity !== undefined ? String(serverQuantity) : '1';
        fields['input.quote-selection-input'].checked = id === 1 && serverQuantity !== undefined;
        fields['td:nth-child(2)'].textContent = `SKU${id}`;
        fields['.quote-row-description'].textContent = `Casco ${id}`;
        row.querySelector = key => fields[key];
        return row;
    });
    const cancelLinks = [new Element(), new Element()];
    const document = new Element();
    document.getElementById = id => ids[id];
    document.querySelectorAll = selector => selector === '[data-product-row]' ? rows : cancelLinks;
    document.createElement = () => new Element();
    const window = new Element();
    const sessionStorage = { getItem: key => storage.get(key) ?? null,
        setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) };
    runInNewContext(script, { document, window, sessionStorage, Intl });
    const choose = (id = 1) => {
        ids.filtroProductos.fire('focus');
        ids.resultadosBusqueda.children[id - 1].fire('click');
    };
    const add = (value, id = 1) => {
        choose(id); ids.cantidadAgregar.value = String(value); ids.agregarProducto.fire('click');
    };
    return { ids, rows, storage, window, cancelLinks, choose, add, key: `prosegin.quote.cart.v2.${clientId}` };
}

test('Agregar muestra datos del catálogo, cantidad, contador y total', () => {
    const ui = screen(); ui.choose();
    assert.equal(ui.ids.productoSeleccionadoDesc.value, 'SKU1 - Casco 1');
    assert.equal(ui.ids.unidadSeleccionada.value, 'UND');
    assert.equal(ui.ids.precioSeleccionado.value, 'S/ 10.00');
    ui.ids.cantidadAgregar.value = '2'; ui.ids.agregarProducto.fire('click');
    assert.equal(ui.rows[0].querySelector('input[name$=".Cantidad"]').value, '2');
    assert.equal(ui.ids.cantidadProductos.textContent, '1');
    assert.equal(ui.ids.subtotalCotizacion.textContent, 'S/ 20.00');
    assert.equal(ui.ids.igvCotizacion.textContent, 'S/ 3.60');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 23.60');
});

test('Repetir acumula 2 + 3 = 5 en una sola fila', () => {
    const ui = screen(); ui.add(2); ui.add(3);
    assert.equal(ui.rows[0].querySelector('input[name$=".Cantidad"]').value, '5');
    assert.equal(ui.ids.cantidadProductos.textContent, '1');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 59.00');
});

for (const value of ['0', '-2', '', '1.5', '2.8', '2147483648', '1e3']) {
    test(`Agregar rechaza ${JSON.stringify(value)} sin cambiar la lista ni la cantidad solicitada`, () => {
        const ui = screen(); ui.add(2); ui.add(value);
        assert.equal(ui.rows[0].querySelector('input[name$=".Cantidad"]').value, '2');
        assert.equal(ui.ids.cantidadAgregar.value, value);
        assert.equal(ui.ids.errorCantidadAgregar.hidden, false);
        assert.match(ui.ids.errorCantidadAgregar.textContent, /entero mayor a cero/);
        assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 23.60');
    });
}

test('La suma no puede desbordar el entero del servidor', () => {
    const ui = screen(); ui.add(2147483647); ui.add(1);
    assert.equal(ui.rows[0].querySelector('input[name$=".Cantidad"]').value, '2147483647');
    assert.equal(ui.ids.errorCantidadAgregar.hidden, false);
});

test('Limpiar restablece campos, cantidades, contador, total y borrador', () => {
    const ui = screen(); ui.add(2); ui.choose(2); ui.ids.cantidadAgregar.value = '8';
    ui.ids.limpiarProductos.fire('click');
    for (const id of ['filtroProductos', 'selectorProducto', 'productoSeleccionadoDesc', 'unidadSeleccionada', 'precioSeleccionado']) assert.equal(ui.ids[id].value, '');
    assert.equal(ui.ids.cantidadAgregar.value, '1');
    assert.equal(ui.ids.cantidadProductos.textContent, '0');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 0.00');
    assert.equal(ui.ids.resultadosBusqueda.hidden, true);
    assert.equal(ui.ids.agregarProducto.disabled, true);
    assert.equal(ui.storage.has(ui.key), false);
    assert.ok(ui.rows.every(row => row.querySelector('input[name$=".Cantidad"]').disabled));
});

for (const index of [0, 1]) {
    test(`Salir por el enlace ${index} descarta el borrador incluso con caché de navegación`, () => {
        const ui = screen(); ui.add(2); ui.cancelLinks[index].fire('click');
        assert.equal(ui.storage.has(ui.key), false);
        ui.window.fire('pageshow', { persisted: true });
        assert.equal(ui.ids.cantidadProductos.textContent, '0');
        assert.equal(screen({ storage: ui.storage }).ids.cantidadProductos.textContent, '0');
    });
}

test('Eliminar retira toda la fila y recalcula contador y total', () => {
    const ui = screen(); ui.add(2); ui.add(1, 2);
    ui.rows[0].querySelector('[data-remove-product]').fire('click');
    assert.equal(ui.rows[0].classList.contains('d-none'), true);
    assert.equal(ui.ids.cantidadProductos.textContent, '1');
    assert.equal(ui.rows[1].querySelector('[data-row-number]').textContent, '1');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 0.30');
});

test('Los borradores se conservan al recargar y se aíslan por cliente', () => {
    const ui = screen(); ui.add(2);
    const other = screen({ storage: ui.storage, clientId: 2 });
    assert.equal(other.ids.cantidadProductos.textContent, '0');
    assert.equal(screen({ storage: ui.storage }).ids.totalCotizacion.textContent, 'S/ 23.60');
    other.add(3); other.cancelLinks[1].fire('click');
    assert.equal(screen({ storage: ui.storage }).ids.totalCotizacion.textContent, 'S/ 23.60');
});

test('Guardado exitoso borra solo el borrador del cliente actual', () => {
    const ui = screen(); ui.add(2);
    screen({ storage: ui.storage, clientId: 2 }).add(3);
    const saved = screen({ storage: ui.storage, saved: true });
    assert.equal(saved.ids.cantidadProductos.textContent, '0');
    assert.equal(saved.storage.has(ui.key), false);
    assert.equal(screen({ storage: ui.storage, clientId: 2 }).ids.cantidadProductos.textContent, '1');
});

test('Un error del servidor conserva la selección devuelta y no restaura un borrador viejo', () => {
    const ui = screen(); ui.add(8);
    const rejected = screen({ storage: ui.storage, invalidPost: true, serverQuantity: 2 });
    assert.equal(rejected.ids.totalCotizacion.textContent, 'S/ 23.60');
    const empty = screen({ storage: ui.storage, invalidPost: true });
    assert.equal(empty.ids.cantidadProductos.textContent, '0');
});

test('Editar una fila con cantidad inválida bloquea el envío sin corregirla ni guardar una lista parcial', () => {
    const ui = screen(); ui.add(2); ui.add(1, 2);
    const oldCart = ui.storage.get(ui.key);
    const input = ui.rows[0].querySelector('input[name$=".Cantidad"]');
    input.value = '1.5'; input.fire('input');
    assert.equal(input.value, '1.5');
    assert.equal(ui.ids.productosError.hidden, false);
    assert.equal(ui.ids.totalCotizacion.textContent, '—');
    assert.equal(ui.storage.get(ui.key), oldCart);
    assert.equal(ui.ids.formCotizacion.fire('submit').defaultPrevented, true);
    input.value = '3'; input.fire('input');
    assert.equal(ui.ids.productosError.hidden, true);
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 35.70');
    assert.equal(ui.ids.formCotizacion.fire('submit').defaultPrevented, false);
});

test('No se permite grabar una lista vacía', () => {
    const ui = screen();
    assert.equal(ui.ids.formCotizacion.fire('submit').defaultPrevented, true);
    assert.match(ui.ids.productosError.textContent, /al menos un producto/);
});

test('Modificar la búsqueda invalida el producto previamente elegido', () => {
    const ui = screen(); ui.choose(); ui.ids.filtroProductos.value = 'inexistente';
    ui.ids.filtroProductos.fire('input');
    assert.equal(ui.ids.selectorProducto.value, '');
    assert.equal(ui.ids.agregarProducto.disabled, true);
    assert.equal(ui.ids.resultadosBusqueda.children[0].textContent, 'No se encontraron productos.');
});

test('Descarta el borrador antiguo compartido y tolera datos corruptos', () => {
    const storage = new Map([['prosegin.quote.cart.v1', '[{"id":1,"quantity":8}]'],
        ['prosegin.quote.cart.v2.1', '{malformado']]);
    const ui = screen({ storage });
    assert.equal(ui.ids.cantidadProductos.textContent, '0');
    assert.equal(storage.size, 0);
});

test('La selección pendiente del catálogo se asigna una sola vez al cliente elegido', () => {
    const storage = new Map([
        ['prosegin.quote.pending.v1', '[{"id":1,"quantity":2}]'],
        ['prosegin.quote.cart.v2.1', '[{"id":1,"quantity":8}]'],
        ['prosegin.quote.cart.v2.2', '[{"id":2,"quantity":3}]']
    ]);
    assert.equal(screen({ storage }).ids.totalCotizacion.textContent, 'S/ 23.60');
    assert.equal(storage.has('prosegin.quote.pending.v1'), false);
    const other = screen({ storage, clientId: 2 });
    assert.equal(other.rows[0].querySelector('input.quote-selection-input').checked, false);
    assert.equal(other.rows[1].querySelector('input[name$=".Cantidad"]').value, '3');
});

function catalogScreen(storage = new Map()) {
    const view = readFileSync(join(__dirname, '../../Prosegin.Web/Views/Catalogo/Index.cshtml'), 'utf8');
    const catalogScript = view.match(/<script>\s*([\s\S]*?)<\/script>/)[1]
        .replace('@Url.Action("Index", "Clientes")', '/Clientes');
    const ids = {};
    for (const id of ['catalogQuantityError', 'cartItems', 'cartEmpty', 'cartItemCount', 'cartSubtotal',
        'cartTax', 'cartTotal', 'continueToClient', 'clearCart', 'catalogSearch', 'catalogNoMatch',
        'catalogProductCount', 'catalogProductLabel']) ids[id] = new Element();
    ids.cartItems.querySelectorAll = () => ids.cartItems.children.filter(child => 'cartLine' in child.dataset);
    const cards = [1, 2].map(id => {
        const card = new Element();
        card.dataset = { productId: String(id), name: `Casco ${id}`, sku: `SKU${id}`,
            cost: id === 1 ? '10' : '0.25', price: id === 1 ? '13' : '0.33', category: 'EPP', search: `sku${id} casco` };
        card.amount = new Element(); card.amount.value = '1'; card.add = new Element();
        card.querySelector = selector => selector === '[data-quantity]' ? card.amount : card.add;
        return card;
    });
    const document = { getElementById: id => ids[id], createElement: () => new Element(),
        querySelectorAll: selector => selector === '[data-product-card]' ? cards : [] };
    const window = new Element(); window.location = { href: '' };
    const sessionStorage = { getItem: key => storage.get(key) ?? null,
        setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) };
    runInNewContext(catalogScript, { document, window, sessionStorage, Intl });
    return { ids, cards, window, storage, add: (value, id = 1) => {
        cards[id - 1].amount.value = String(value); cards[id - 1].add.fire('click');
    } };
}

test('Catálogo: repetir incrementa la cantidad en una sola línea', () => {
    const ui = catalogScreen(); ui.add(2); ui.add(3);
    assert.deepEqual(JSON.parse(ui.storage.get('prosegin.catalog.cart.v1')), [{ id: 1, quantity: 5 }]);
    assert.equal(ui.ids.cartItemCount.textContent, '1');
    assert.equal(ui.ids.cartTotal.textContent, 'S/ 76.70');
});

for (const value of ['0', '-2', '', '1.5', '2.8', '2147483648', '1e3']) {
    test(`Catálogo: rechaza ${JSON.stringify(value)} sin alterar la selección`, () => {
        const ui = catalogScreen(); ui.add(2); const before = ui.storage.get('prosegin.catalog.cart.v1');
        ui.add(value);
        assert.equal(ui.storage.get('prosegin.catalog.cart.v1'), before);
        assert.equal(ui.cards[0].amount.value, value);
        assert.equal(ui.ids.catalogQuantityError.hidden, false);
    });
}

test('Catálogo -> Clientes -> Cotización: conserva cantidades y no las transfiere a otro cliente', () => {
    const ui = catalogScreen(); ui.add(2); ui.add(1, 2); ui.ids.continueToClient.fire('click');
    assert.equal(ui.window.location.href, '/Clientes');
    assert.equal(ui.storage.has('prosegin.catalog.cart.v1'), false);
    const quote = screen({ storage: ui.storage });
    assert.equal(quote.ids.cantidadProductos.textContent, '2');
    assert.equal(quote.ids.totalCotizacion.textContent, 'S/ 23.90');
    assert.equal(screen({ storage: ui.storage, clientId: 2 }).ids.cantidadProductos.textContent, '0');
    quote.cancelLinks[1].fire('click');
    assert.equal(screen({ storage: ui.storage }).ids.cantidadProductos.textContent, '0');
    ui.window.fire('pageshow', { persisted: true });
    assert.equal(ui.ids.cartItemCount.textContent, '0');
});

test('Catálogo: vaciar elimina productos y restablece cantidades', () => {
    const ui = catalogScreen(); ui.add(2); ui.cards[1].amount.value = '9'; ui.ids.clearCart.fire('click');
    assert.equal(ui.ids.cartItemCount.textContent, '0');
    assert.equal(ui.ids.cartTotal.textContent, 'S/ 0.00');
    assert.equal(ui.ids.continueToClient.disabled, true);
    assert.ok(ui.cards.every(card => card.amount.value === '1'));
});

test('El IGV redondea los medios céntimos igual que el servidor decimal', () => {
    const ui = screen();
    ui.rows[0].dataset.cost = '30.25';
    ui.rows[0].querySelector('input.quote-price-input').value = '30.25';
    ui.add(1);
    assert.equal(ui.ids.subtotalCotizacion.textContent, 'S/ 30.25');
    assert.equal(ui.ids.igvCotizacion.textContent, 'S/ 5.45');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 35.70');
});

test('Varias filas suman los céntimos antes de calcular el IGV', () => {
    const ui = screen();
    ui.rows[0].dataset.cost = '0.10';
    ui.rows[1].dataset.cost = '0.15';
    ui.rows[0].querySelector('input.quote-price-input').value = '0.10';
    ui.rows[1].querySelector('input.quote-price-input').value = '0.15';
    ui.add(1); ui.add(1, 2);
    assert.equal(ui.ids.cantidadProductos.textContent, '2');
    assert.equal(ui.ids.subtotalCotizacion.textContent, 'S/ 0.25');
    assert.equal(ui.ids.igvCotizacion.textContent, 'S/ 0.05');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 0.30');
});

test('Restaurar ignora productos ausentes y cantidades inválidas del borrador', () => {
    const storage = new Map([['prosegin.quote.cart.v2.1', JSON.stringify([
        { id: 999, quantity: 4 }, { id: 1, quantity: -1 }, { id: 2, quantity: 3 }
    ])]]);
    const ui = screen({ storage });
    assert.equal(ui.ids.cantidadProductos.textContent, '1');
    assert.equal(ui.rows[0].querySelector('input.quote-selection-input').checked, false);
    assert.equal(ui.rows[1].querySelector('input[name$=".Cantidad"]').value, '3');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 0.89');
    assert.deepEqual(JSON.parse(storage.get(ui.key)), [{ id: 2, quantity: 3, price: 0.25 }]);
});

test('HU 3.2: ajustar el precio recalcula subtotal, IGV, total y márgenes', () => {
    const ui = screen(); ui.add(2);
    const input = ui.rows[0].querySelector('input.quote-price-input');
    input.value = '12.50'; input.fire('input');
    assert.equal(ui.ids.subtotalCotizacion.textContent, 'S/ 25.00');
    assert.equal(ui.ids.igvCotizacion.textContent, 'S/ 4.50');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 29.50');
    assert.equal(ui.rows[0].querySelector('.quote-margin-badge').textContent, '+25% margen');
    assert.equal(ui.ids.margenEstimadoCotizacion.textContent, '+25.0% (S/ 5.00)');
    ui.add(3);
    assert.equal(input.value, '12.50');
    assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 73.75');
});

for (const value of ['9.99', '0', '-1', '', '12.501']) {
    test(`HU 3.2: el precio ${JSON.stringify(value)} bloquea el envío sin alterar el costo`, () => {
        const ui = screen(); ui.add(2); const previous = ui.storage.get(ui.key);
        const input = ui.rows[0].querySelector('input.quote-price-input');
        input.value = value; input.fire('input');
        assert.equal(ui.ids.alertaPrecioError.classList.contains('d-none'), false);
        assert.equal(input.value, value);
        assert.equal(ui.ids.formCotizacion.fire('submit').defaultPrevented, true);
        assert.equal(ui.rows[0].dataset.cost, '10');
        assert.equal(ui.storage.get(ui.key), previous);
        input.value = '13'; input.fire('input');
        assert.equal(ui.ids.alertaPrecioError.classList.contains('d-none'), true);
        assert.equal(ui.ids.formCotizacion.fire('submit').defaultPrevented, false);
        assert.equal(ui.ids.totalCotizacion.textContent, 'S/ 30.68');
    });
}

test('HU 3.2: el precio ajustado se restaura por cliente y Limpiar lo restablece al costo', () => {
    const ui = screen(); ui.add(2);
    ui.rows[0].querySelector('input.quote-price-input').value = '12.50';
    ui.rows[0].querySelector('input.quote-price-input').fire('input');
    const restored = screen({ storage: ui.storage });
    assert.equal(restored.ids.totalCotizacion.textContent, 'S/ 29.50');
    assert.equal(restored.rows[0].querySelector('input.quote-price-input').value, '12.50');
    restored.ids.limpiarProductos.fire('click');
    assert.equal(restored.rows[0].querySelector('input.quote-price-input').value, '10.00');
    assert.equal(restored.rows[0].querySelector('input.quote-price-input').disabled, true);
});
