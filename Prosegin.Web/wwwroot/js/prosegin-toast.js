/**
 * ProseginToast - Sistema Modular de Notificaciones Toast para PROSEGIN Core
 * Alertas emergentes minimalistas con auto-desvanecimiento y pausa al interactuar.
 *
 * Métodos:
 *   ProseginToast.success(mensaje, titulo?, duracionMs?)
 *   ProseginToast.error(mensaje, titulo?, duracionMs?)
 *   ProseginToast.warning(mensaje, titulo?, duracionMs?)
 *   ProseginToast.info(mensaje, titulo?, duracionMs?)
 *   ProseginToast.show({ type, message, title, duration })
 */
(function (window) {
    'use strict';

    const CONTAINER_ID = 'prosegin-toast-container';

    const DEFAULT_CONFIGS = {
        success: {
            title: 'Operación Exitosa',
            icon: 'check_circle',
            duration: 3800
        },
        error: {
            title: 'Atención',
            icon: 'cancel',
            duration: 6000
        },
        warning: {
            title: 'Advertencia',
            icon: 'warning',
            duration: 5500
        },
        info: {
            title: 'Información',
            icon: 'info',
            duration: 4000
        }
    };

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function getOrCreateContainer() {
        let container = document.getElementById(CONTAINER_ID);
        if (!container) {
            container = document.createElement('div');
            container.id = CONTAINER_ID;
            container.className = 'prosegin-toast-container';
            container.setAttribute('aria-live', 'polite');
            document.body.appendChild(container);
        }
        return container;
    }

    function show(options) {
        if (!options) return;
        if (typeof options === 'string') {
            options = { message: options, type: 'info' };
        }

        const type = options.type && DEFAULT_CONFIGS[options.type] ? options.type : 'info';
        const defaultConfig = DEFAULT_CONFIGS[type];
        const title = options.title !== undefined && options.title !== null ? options.title : defaultConfig.title;
        const message = options.message || '';
        const duration = typeof options.duration === 'number' ? options.duration : defaultConfig.duration;

        const container = getOrCreateContainer();

        const toast = document.createElement('div');
        toast.className = `prosegin-toast toast-${type}`;
        toast.setAttribute('role', 'alert');

        toast.innerHTML = `
            <div class="prosegin-toast-icon">
                <i class="material-symbols-outlined">${defaultConfig.icon}</i>
            </div>
            <div class="prosegin-toast-content">
                ${title ? `<div class="prosegin-toast-title">${escapeHtml(title)}</div>` : ''}
                <div class="prosegin-toast-message">${escapeHtml(message)}</div>
            </div>
            <button type="button" class="prosegin-toast-close" aria-label="Cerrar">
                <i class="material-symbols-outlined" style="font-size: 1.1rem;">close</i>
            </button>
            <div class="prosegin-toast-progress">
                <div class="prosegin-toast-progress-bar"></div>
            </div>
        `;

        container.appendChild(toast);

        // Animar entrada suave
        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                toast.classList.add('show');
            });
        });

        // Configuración del temporizador de auto-cierre con soporte de pausa al pasar el mouse
        let remainingTime = duration;
        let startTime = Date.now();
        let timerId = null;
        let isClosed = false;
        const progressBar = toast.querySelector('.prosegin-toast-progress-bar');

        function startProgressAnimation(time) {
            if (progressBar) {
                progressBar.style.transition = `transform ${time}ms linear`;
                progressBar.style.transform = 'scaleX(0)';
            }
        }

        function closeToast() {
            if (isClosed) return;
            isClosed = true;
            clearTimeout(timerId);

            toast.classList.remove('show');
            toast.classList.add('hide');

            setTimeout(() => {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                }
            }, 300);
        }

        function startTimer() {
            startTime = Date.now();
            startProgressAnimation(remainingTime);
            timerId = setTimeout(closeToast, remainingTime);
        }

        function pauseTimer() {
            clearTimeout(timerId);
            const elapsed = Date.now() - startTime;
            remainingTime = Math.max(0, remainingTime - elapsed);
            if (progressBar) {
                const computedTransform = window.getComputedStyle(progressBar).transform;
                progressBar.style.transition = 'none';
                progressBar.style.transform = computedTransform;
            }
        }

        startTimer();

        // Pausar auto-cierre si el usuario coloca el cursor encima
        toast.addEventListener('mouseenter', pauseTimer);
        toast.addEventListener('mouseleave', () => {
            if (remainingTime > 0 && !isClosed) {
                startTimer();
            }
        });

        // Botón cerrar manual
        const closeBtn = toast.querySelector('.prosegin-toast-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                closeToast();
            });
        }

        return {
            close: closeToast
        };
    }

    const ProseginToast = {
        show: show,
        success: function (message, title, duration) {
            return show({ type: 'success', message: message, title: title, duration: duration });
        },
        error: function (message, title, duration) {
            return show({ type: 'error', message: message, title: title, duration: duration });
        },
        warning: function (message, title, duration) {
            return show({ type: 'warning', message: message, title: title, duration: duration });
        },
        info: function (message, title, duration) {
            return show({ type: 'info', message: message, title: title, duration: duration });
        }
    };

    window.ProseginToast = ProseginToast;
})(window);
