// ============================================================
// PROSEGIN CORE - COMPORTAMIENTO INTERACTIVO DEL SIDEBAR & LAYOUT
// ============================================================

document.addEventListener('DOMContentLoaded', function () {
    const appContainer = document.querySelector('.prosegin-app-container');
    const sidebar = document.getElementById('proseginSidebar');
    const backdrop = document.getElementById('sidebarBackdrop');
    const btnToggle = document.getElementById('btnSidebarToggle');
    const brandMini = document.querySelector('.sidebar-brand-mini');

    const STORAGE_KEY = 'prosegin_sidebar_collapsed';

    // 1. Asegurar que todos los enlaces del menú tengan su tag flotante
    document.querySelectorAll('.sidebar-nav-link').forEach(function (link) {
        if (!link.querySelector('.sidebar-floating-tag')) {
            const textSpan = link.querySelector('.sidebar-nav-text') || link.querySelector('span');
            if (textSpan) {
                const tag = document.createElement('span');
                tag.className = 'sidebar-floating-tag';
                tag.textContent = textSpan.textContent.trim();
                link.appendChild(tag);
            }
        }
    });

    // 2. Funciones de colapso/expansión en escritorio
    function isDesktop() {
        return window.innerWidth >= 992;
    }

    function updateToggleButton(collapsed) {
        if (btnToggle) {
            btnToggle.setAttribute('title', collapsed ? 'Expandir menú lateral' : 'Contraer menú lateral');
            btnToggle.setAttribute('aria-label', collapsed ? 'Expandir menú lateral' : 'Contraer menú lateral');
        }
    }

    function setSidebarCollapsed(collapsed) {
        if (!appContainer) return;

        if (collapsed) {
            appContainer.classList.add('sidebar-collapsed');
            try {
                localStorage.setItem(STORAGE_KEY, 'true');
            } catch (e) {}
        } else {
            appContainer.classList.remove('sidebar-collapsed');
            try {
                localStorage.setItem(STORAGE_KEY, 'false');
            } catch (e) {}
        }

        updateToggleButton(collapsed);
    }

    function toggleSidebarCollapse() {
        if (!appContainer) return;
        const willCollapse = !appContainer.classList.contains('sidebar-collapsed');
        setSidebarCollapsed(willCollapse);
    }

    // Inicializar estado según preferencia guardada en localStorage
    try {
        const savedCollapsed = localStorage.getItem(STORAGE_KEY) === 'true';
        if (savedCollapsed && isDesktop()) {
            setSidebarCollapsed(true);
        } else {
            updateToggleButton(false);
        }
    } catch (e) {
        // En caso de que localStorage no esté disponible
    } finally {
        // Remover clase preliminar anti-flicker
        document.documentElement.classList.remove('sidebar-is-collapsed-init');
    }

    // 3. Funciones para Drawer en pantallas móviles (< 992px)
    function openMobileSidebar() {
        if (sidebar) sidebar.classList.add('show');
        if (backdrop) backdrop.classList.add('show');
    }

    function closeMobileSidebar() {
        if (sidebar) sidebar.classList.remove('show');
        if (backdrop) backdrop.classList.remove('show');
    }

    // Eventos de botones
    if (btnToggle) {
        btnToggle.addEventListener('click', function (e) {
            e.preventDefault();
            if (isDesktop()) {
                toggleSidebarCollapse();
            } else {
                openMobileSidebar();
            }
        });
    }

    // Hacer clic en el isotipo cuando está contraído también lo expande
    if (brandMini) {
        brandMini.addEventListener('click', function (e) {
            if (isDesktop() && appContainer && appContainer.classList.contains('sidebar-collapsed')) {
                e.preventDefault();
                setSidebarCollapsed(false);
            }
        });
    }

    if (backdrop) {
        backdrop.addEventListener('click', closeMobileSidebar);
    }

    // Adaptar cuando se redimensiona la ventana
    window.addEventListener('resize', function () {
        if (!isDesktop()) {
            if (sidebar && !sidebar.classList.contains('show')) {
                closeMobileSidebar();
            }
        } else {
            try {
                const savedCollapsed = localStorage.getItem(STORAGE_KEY) === 'true';
                if (savedCollapsed) {
                    setSidebarCollapsed(true);
                }
            } catch (e) {}
        }
    });
});
