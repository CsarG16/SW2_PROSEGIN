/*
 * PROSEGIN - Dropdown personalizado (sin librerías).
 * Mejora cada <select> nativo: lo deja oculto (sigue siendo el que se envía en el formulario,
 * valida y dispara "change") y dibuja un menú propio con el estilo del sistema.
 * Para excluir un select: <select data-native="true">. Los <select multiple> no se tocan.
 */
(function () {
    'use strict';

    var SKIP = 'select[multiple], select[data-native], select[size]:not([size="1"])';
    var enhanced = new WeakMap();
    var openInstance = null;
    var uid = 0;

    function Dropdown(select) {
        this.select = select;
        this.id = 'ps-select-' + (++uid);
        this.activeIndex = -1;
        this.typeBuffer = '';
        this.typeTimer = null;
        this.build();
        this.refresh();
    }

    Dropdown.prototype.build = function () {
        var s = this.select;
        var self = this;

        var wrap = document.createElement('div');
        wrap.className = 'ps-select';
        if (s.style.width) wrap.style.width = s.style.width;
        if (s.style.maxWidth) wrap.style.maxWidth = s.style.maxWidth;
        if (s.style.minWidth) wrap.style.minWidth = s.style.minWidth;
        s.parentNode.insertBefore(wrap, s);
        wrap.appendChild(s);

        var trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = (s.className.indexOf('form-select') === -1 ? 'form-select ' : '') + s.className + ' ps-select-trigger';
        trigger.setAttribute('role', 'combobox');
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.setAttribute('aria-expanded', 'false');
        trigger.setAttribute('aria-controls', this.id);
        var label = s.getAttribute('aria-label');
        if (label) trigger.setAttribute('aria-label', label);

        var text = document.createElement('span');
        text.className = 'ps-select-text';
        trigger.appendChild(text);
        wrap.appendChild(trigger);

        s.classList.add('ps-native');
        s.tabIndex = -1;
        s.setAttribute('aria-hidden', 'true');

        this.wrap = wrap;
        this.trigger = trigger;
        this.text = text;
        this.menu = null;

        trigger.addEventListener('click', function () { self.isOpen() ? self.close() : self.open(); });
        trigger.addEventListener('keydown', function (e) { self.onTriggerKey(e); });
        s.addEventListener('focus', function () { trigger.focus(); });
        s.addEventListener('invalid', function () { trigger.classList.add('is-invalid'); });
        s.addEventListener('change', function () { self.refresh(); });
    };

    Dropdown.prototype.isOpen = function () { return !!this.menu; };

    Dropdown.prototype.items = function () {
        var list = [];
        Array.prototype.forEach.call(this.select.children, function (node) {
            if (node.tagName === 'OPTGROUP') {
                list.push({ group: node.label, disabled: node.disabled });
                Array.prototype.forEach.call(node.children, function (o) {
                    list.push({ option: o, disabled: o.disabled || node.disabled, indented: true });
                });
            } else if (node.tagName === 'OPTION') {
                list.push({ option: node, disabled: node.disabled });
            }
        });
        return list;
    };

    Dropdown.prototype.refresh = function () {
        var s = this.select;
        var sel = s.options[s.selectedIndex];
        var txt = sel ? sel.textContent.trim() : '';
        var isPlaceholder = !sel || (sel.value === '' && s.selectedIndex === 0 && !!txt);
        this.text.textContent = txt || ' ';
        this.text.classList.toggle('is-placeholder', isPlaceholder);
        this.trigger.disabled = s.disabled;
        ['is-invalid', 'is-valid', 'input-validation-error'].forEach(function (c) {
            this.trigger.classList.toggle(c, s.classList.contains(c));
        }, this);
        if (this.isOpen()) this.renderMenu();
    };

    Dropdown.prototype.open = function () {
        if (this.select.disabled || this.isOpen()) return;
        if (openInstance && openInstance !== this) openInstance.close();
        openInstance = this;

        var menu = document.createElement('div');
        menu.className = 'ps-select-menu';
        menu.id = this.id;
        menu.setAttribute('role', 'listbox');
        menu.tabIndex = -1;
        this.menu = menu;
        document.body.appendChild(menu);
        this.trigger.setAttribute('aria-expanded', 'true');
        this.trigger.classList.add('is-open');

        this.renderMenu();
        this.position();
        var active = this.menu.querySelector('.is-selected') || this.menu.querySelector('.ps-select-option:not(.is-disabled)');
        this.setActive(active);
        var search = this.menu.querySelector('.ps-select-search input');
        if (search) search.focus({ preventScroll: true });
    };

    Dropdown.prototype.close = function (focusTrigger) {
        if (!this.menu) return;
        this.menu.remove();
        this.menu = null;
        this.trigger.setAttribute('aria-expanded', 'false');
        this.trigger.classList.remove('is-open');
        if (openInstance === this) openInstance = null;
        if (focusTrigger !== false) this.trigger.focus({ preventScroll: true });
    };

    Dropdown.prototype.renderMenu = function () {
        var self = this;
        var menu = this.menu;
        var items = this.items();
        var optionCount = items.filter(function (i) { return i.option; }).length;
        var currentSearch = menu.querySelector('.ps-select-search input');
        var term = currentSearch ? currentSearch.value : '';
        menu.textContent = '';

        if (optionCount > 12) {
            var searchWrap = document.createElement('div');
            searchWrap.className = 'ps-select-search';
            var input = document.createElement('input');
            input.type = 'text';
            input.placeholder = 'Buscar...';
            input.autocomplete = 'off';
            input.value = term;
            input.setAttribute('aria-label', 'Buscar opción');
            input.addEventListener('input', function () { self.filter(input.value); });
            input.addEventListener('keydown', function (e) { self.onMenuKey(e); });
            searchWrap.appendChild(input);
            menu.appendChild(searchWrap);
        }

        var list = document.createElement('div');
        list.className = 'ps-select-list';
        menu.appendChild(list);

        items.forEach(function (item) {
            var el = document.createElement('div');
            if (item.group !== undefined) {
                el.className = 'ps-select-group';
                el.textContent = item.group;
                list.appendChild(el);
                return;
            }
            var o = item.option;
            el.className = 'ps-select-option' + (item.indented ? ' is-indented' : '') + (item.disabled ? ' is-disabled' : '') + (o.selected ? ' is-selected' : '');
            el.setAttribute('role', 'option');
            el.setAttribute('aria-selected', o.selected ? 'true' : 'false');
            el._option = o;
            var label = document.createElement('span');
            label.textContent = o.textContent.trim() || ' ';
            el.appendChild(label);
            if (o.value === '' && !o.textContent.trim()) el.classList.add('is-empty');
            if (o.selected) {
                var check = document.createElement('i');
                check.className = 'material-symbols-outlined ps-select-check';
                check.textContent = 'check';
                el.appendChild(check);
            }
            el.addEventListener('mousedown', function (e) { e.preventDefault(); });
            el.addEventListener('click', function () { if (!item.disabled) self.choose(o); });
            el.addEventListener('mousemove', function () { if (!item.disabled) self.setActive(el, true); });
            list.appendChild(el);
        });

        var empty = document.createElement('div');
        empty.className = 'ps-select-empty';
        empty.textContent = 'Sin resultados';
        empty.hidden = true;
        list.appendChild(empty);

        menu.addEventListener('keydown', function (e) { self.onMenuKey(e); });
        if (term) this.filter(term);
    };

    Dropdown.prototype.filter = function (term) {
        var t = term.trim().toLowerCase();
        var any = false;
        var group = null, groupHasVisible = false;
        Array.prototype.forEach.call(this.menu.querySelectorAll('.ps-select-group, .ps-select-option'), function (el) {
            if (el.classList.contains('ps-select-group')) {
                if (group) group.hidden = !groupHasVisible;
                group = el; groupHasVisible = false; return;
            }
            var show = !t || el.textContent.toLowerCase().indexOf(t) !== -1;
            el.hidden = !show;
            if (show) { any = true; groupHasVisible = true; }
        });
        if (group) group.hidden = !groupHasVisible;
        this.menu.querySelector('.ps-select-empty').hidden = any;
        this.setActive(this.menu.querySelector('.ps-select-option:not([hidden]):not(.is-disabled)'));
    };

    Dropdown.prototype.visibleOptions = function () {
        return Array.prototype.filter.call(this.menu.querySelectorAll('.ps-select-option'), function (el) {
            return !el.hidden && !el.classList.contains('is-disabled');
        });
    };

    Dropdown.prototype.setActive = function (el, noScroll) {
        if (!this.menu) return;
        var prev = this.menu.querySelector('.is-active');
        if (prev) prev.classList.remove('is-active');
        if (!el) return;
        el.classList.add('is-active');
        if (!noScroll) el.scrollIntoView({ block: 'nearest' });
    };

    Dropdown.prototype.move = function (delta) {
        var opts = this.visibleOptions();
        if (!opts.length) return;
        var idx = opts.indexOf(this.menu.querySelector('.is-active'));
        idx = idx === -1 ? (delta > 0 ? 0 : opts.length - 1) : Math.max(0, Math.min(opts.length - 1, idx + delta));
        this.setActive(opts[idx]);
    };

    Dropdown.prototype.choose = function (option) {
        var s = this.select;
        var changed = s.value !== option.value || !option.selected;
        option.selected = true;
        this.refresh();
        this.close();
        if (changed) {
            s.dispatchEvent(new Event('input', { bubbles: true }));
            s.dispatchEvent(new Event('change', { bubbles: true }));
        }
    };

    Dropdown.prototype.onMenuKey = function (e) {
        var k = e.key;
        if (k === 'ArrowDown') { e.preventDefault(); this.move(1); }
        else if (k === 'ArrowUp') { e.preventDefault(); this.move(-1); }
        else if (k === 'Home') { e.preventDefault(); this.setActive(this.visibleOptions()[0]); }
        else if (k === 'End') { e.preventDefault(); var v = this.visibleOptions(); this.setActive(v[v.length - 1]); }
        else if (k === 'Enter') { e.preventDefault(); var a = this.menu.querySelector('.is-active'); if (a) this.choose(a._option); }
        else if (k === 'Escape') { e.preventDefault(); e.stopPropagation(); this.close(); }
        else if (k === 'Tab') { this.close(false); }
    };

    Dropdown.prototype.onTriggerKey = function (e) {
        var k = e.key;
        if (k === 'ArrowDown' || k === 'ArrowUp' || k === 'Enter' || k === ' ') {
            e.preventDefault();
            if (!this.isOpen()) this.open(); else this.onMenuKey(e);
            return;
        }
        if (this.isOpen()) { this.onMenuKey(e); return; }
        if (k.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) this.typeAhead(k);
    };

    Dropdown.prototype.typeAhead = function (ch) {
        var self = this;
        clearTimeout(this.typeTimer);
        this.typeBuffer += ch.toLowerCase();
        this.typeTimer = setTimeout(function () { self.typeBuffer = ''; }, 600);
        var match = this.items().filter(function (i) {
            return i.option && !i.disabled && i.option.textContent.trim().toLowerCase().indexOf(self.typeBuffer) === 0;
        })[0];
        if (match) this.choose(match.option);
    };

    Dropdown.prototype.position = function () {
        var menu = this.menu;
        var r = this.trigger.getBoundingClientRect();
        var gap = 4;
        menu.style.minWidth = r.width + 'px';
        menu.style.maxWidth = Math.max(r.width, Math.min(480, window.innerWidth - 16)) + 'px';
        var h = menu.offsetHeight;
        var below = window.innerHeight - r.bottom - gap - 8;
        var above = r.top - gap - 8;
        var openUp = h > below && above > below;
        var maxH = Math.max(140, openUp ? above : below);
        menu.style.maxHeight = Math.min(maxH, 320) + 'px';
        h = menu.offsetHeight;
        menu.style.left = Math.max(8, Math.min(r.left, window.innerWidth - menu.offsetWidth - 8)) + 'px';
        menu.style.top = (openUp ? r.top - h - gap : r.bottom + gap) + 'px';
    };

    function enhance(select) {
        if (enhanced.has(select) || select.matches(SKIP) || select.closest('template')) return;
        if (!select.parentNode) return;
        enhanced.set(select, new Dropdown(select));
    }

    function scan(root) {
        if (root.nodeType !== 1) return;
        if (root.matches && root.matches('select')) enhance(root);
        if (root.querySelectorAll) Array.prototype.forEach.call(root.querySelectorAll('select'), enhance);
    }

    function sync(select) {
        var d = enhanced.get(select);
        if (d) d.refresh();
    }

    // Cambios hechos por código (select.value = "...", selectedIndex = n) no disparan eventos: se sincronizan aquí.
    ['value', 'selectedIndex'].forEach(function (prop) {
        var desc = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, prop);
        if (!desc || !desc.set) return;
        Object.defineProperty(HTMLSelectElement.prototype, prop, {
            configurable: true,
            enumerable: desc.enumerable,
            get: desc.get,
            set: function (v) { desc.set.call(this, v); sync(this); }
        });
    });

    function init() {
        scan(document.body);

        new MutationObserver(function (mutations) {
            mutations.forEach(function (m) {
                if (m.type === 'childList') {
                    Array.prototype.forEach.call(m.addedNodes, scan);
                    var sel = m.target.closest ? m.target.closest('select') : null;
                    if (sel) sync(sel);
                } else if (m.type === 'attributes') {
                    var s = m.target.closest ? m.target.closest('select') : null;
                    if (s) sync(s);
                }
            });
        }).observe(document.body, {
            childList: true, subtree: true,
            attributes: true, attributeFilter: ['disabled', 'selected', 'class', 'label']
        });

        document.addEventListener('mousedown', function (e) {
            if (openInstance && !openInstance.menu.contains(e.target) && !openInstance.wrap.contains(e.target)) openInstance.close(false);
        });
        document.addEventListener('reset', function (e) {
            setTimeout(function () {
                Array.prototype.forEach.call(e.target.querySelectorAll('select'), sync);
            }, 0);
        });
        window.addEventListener('resize', function () { if (openInstance) openInstance.close(false); });
        window.addEventListener('scroll', function (e) {
            if (openInstance && !openInstance.menu.contains(e.target)) openInstance.close(false);
        }, true);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

    window.PROSEGINSelect = { refresh: function (select) { sync(select); } };
})();
