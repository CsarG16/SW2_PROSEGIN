// Sidebar toggle logic for mobile and responsive views
document.addEventListener('DOMContentLoaded', function () {
    const sidebar = document.getElementById('proseginSidebar');
    const backdrop = document.getElementById('sidebarBackdrop');
    const btnToggle = document.getElementById('btnSidebarToggle');
    const btnClose = document.getElementById('btnSidebarClose');

    function openSidebar() {
        if (sidebar) sidebar.classList.add('show');
        if (backdrop) backdrop.classList.add('show');
    }

    function closeSidebar() {
        if (sidebar) sidebar.classList.remove('show');
        if (backdrop) backdrop.classList.remove('show');
    }

    if (btnToggle) {
        btnToggle.addEventListener('click', openSidebar);
    }

    if (btnClose) {
        btnClose.addEventListener('click', closeSidebar);
    }

    if (backdrop) {
        backdrop.addEventListener('click', closeSidebar);
    }
});
