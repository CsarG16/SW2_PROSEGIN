/**
 * ProseginAlert - Sistema Unificado de Alertas y Popups Emergentes para PROSEGIN Core
 * Cumple con el Design System corporativo y permite llamadas directas en cualquier parte del sistema.
 * 
 * Métodos disponibles:
 *   ProseginAlert.warning(mensaje, titulo?)
 *   ProseginAlert.error(mensaje, titulo?)
 *   ProseginAlert.success(mensaje, titulo?)
 *   ProseginAlert.info(mensaje, titulo?)
 *   ProseginAlert.confirm(mensaje, titulo?, confirmText?, cancelText?) -> Promise<boolean>
 *   ProseginAlert.show(opciones)
 */
(function (window) {
    'use strict';

    const ICONS = {
        warning: 'bi-exclamation-triangle-fill',
        error: 'bi-x-octagon-fill',
        success: 'bi-check-circle-fill',
        info: 'bi-info-circle-fill'
    };

    const TITLES = {
        warning: 'Advertencia',
        error: 'Error',
        success: 'Operación Exitosa',
        info: 'Información'
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

    function show(options) {
        return new Promise((resolve) => {
            const config = Object.assign({
                type: 'warning', // warning | error | success | info
                title: null,
                message: '',
                confirmText: 'Entendido',
                cancelText: 'Cancelar',
                showCancel: false,
                closeOnBackdrop: false
            }, typeof options === 'string' ? { message: options } : options);

            const type = config.type || 'warning';
            const title = config.title !== null && config.title !== undefined ? config.title : (TITLES[type] || 'Aviso');
            const iconClass = ICONS[type] || ICONS.warning;

            // Remover cualquier alerta previa abierta
            const prev = document.querySelector('.prosegin-alert-backdrop');
            if (prev) prev.remove();

            // Crear backdrop y modal
            const backdrop = document.createElement('div');
            backdrop.className = 'prosegin-alert-backdrop';
            backdrop.setAttribute('role', 'dialog');
            backdrop.setAttribute('aria-modal', 'true');

            const btnConfirmClass = type === 'error' ? 'prosegin-alert-btn-confirm btn-error' : 'prosegin-alert-btn-confirm';

            backdrop.innerHTML = `
                <div class="prosegin-alert-modal">
                    <div class="prosegin-alert-icon-wrapper ${type}">
                        <i class="bi ${iconClass}"></i>
                    </div>
                    <h5 class="prosegin-alert-title">${escapeHtml(title)}</h5>
                    <div class="prosegin-alert-message">${config.message}</div>
                    <div class="prosegin-alert-actions">
                        ${config.showCancel ? `<button type="button" class="prosegin-alert-btn prosegin-alert-btn-cancel" id="btnProseginAlertCancel">${escapeHtml(config.cancelText)}</button>` : ''}
                        <button type="button" class="prosegin-alert-btn ${btnConfirmClass}" id="btnProseginAlertConfirm">${escapeHtml(config.confirmText)}</button>
                    </div>
                </div>
            `;

            document.body.appendChild(backdrop);

            // Animación de entrada
            requestAnimationFrame(() => {
                backdrop.classList.add('show');
            });

            const btnConfirm = backdrop.querySelector('#btnProseginAlertConfirm');
            const btnCancel = backdrop.querySelector('#btnProseginAlertCancel');

            function cleanup(result) {
                backdrop.classList.remove('show');
                document.removeEventListener('keydown', handleKeyDown);
                setTimeout(() => {
                    if (backdrop.parentNode) {
                        backdrop.parentNode.removeChild(backdrop);
                    }
                    resolve(result);
                }, 220);
            }

            function handleKeyDown(e) {
                if (e.key === 'Escape') {
                    e.preventDefault();
                    cleanup(false);
                } else if (e.key === 'Enter') {
                    e.preventDefault();
                    cleanup(true);
                }
            }

            if (btnConfirm) {
                btnConfirm.focus();
                btnConfirm.addEventListener('click', () => cleanup(true));
            }

            if (btnCancel) {
                btnCancel.addEventListener('click', () => cleanup(false));
            }

            if (config.closeOnBackdrop) {
                backdrop.addEventListener('click', (e) => {
                    if (e.target === backdrop) cleanup(false);
                });
            }

            document.addEventListener('keydown', handleKeyDown);
        });
    }

    const ProseginAlert = {
        show: show,
        warning: function (message, title = 'Advertencia') {
            return show({ type: 'warning', title: title, message: message, confirmText: 'Entendido' });
        },
        error: function (message, title = 'Error') {
            return show({ type: 'error', title: title, message: message, confirmText: 'Aceptar' });
        },
        success: function (message, title = 'Operación Exitosa') {
            return show({ type: 'success', title: title, message: message, confirmText: 'Aceptar' });
        },
        info: function (message, title = 'Información') {
            return show({ type: 'info', title: title, message: message, confirmText: 'Entendido' });
        },
        confirm: function (message, title = '¿Está seguro?', confirmText = 'Confirmar', cancelText = 'Cancelar') {
            return show({
                type: 'warning',
                title: title,
                message: message,
                showCancel: true,
                confirmText: confirmText,
                cancelText: cancelText
            });
        }
    };

    window.ProseginAlert = ProseginAlert;
})(window);
