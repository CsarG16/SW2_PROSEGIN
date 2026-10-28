(function (window) {
    'use strict';

    const pdfJsUrl = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.min.mjs';
    const workerUrl = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.worker.min.mjs';
    let pdfJsPromise;

    function loadPdfJs() {
        if (!pdfJsPromise) {
            pdfJsPromise = import(pdfJsUrl).then(pdfjs => {
                pdfjs.GlobalWorkerOptions.workerSrc = workerUrl;
                return pdfjs;
            });
        }
        return pdfJsPromise;
    }

    async function mount(options) {
        const canvas = options.canvas;
        const source = options.source;
        if (!canvas || !source) return null;

        const context = canvas.getContext('2d');
        const pdfjs = await loadPdfJs();
        const loadingTask = pdfjs.getDocument(source);
        const pdf = await loadingTask.promise;
        let pageNumber = 1;
        let zoom = 1;

        async function render() {
            const page = await pdf.getPage(pageNumber);
            const viewport = page.getViewport({ scale: zoom });
            const ratio = window.devicePixelRatio || 1;
            canvas.width = Math.floor(viewport.width * ratio);
            canvas.height = Math.floor(viewport.height * ratio);
            canvas.style.width = `${Math.floor(viewport.width)}px`;
            canvas.style.height = `${Math.floor(viewport.height)}px`;
            context.setTransform(ratio, 0, 0, ratio, 0, 0);
            await page.render({ canvasContext: context, viewport }).promise;
            options.onChange?.(pageNumber, pdf.numPages, Math.round(zoom * 100));
        }

        await render();
        return {
            previous: async () => {
                if (pageNumber <= 1) return false;
                pageNumber--;
                await render();
                return true;
            },
            next: async () => {
                if (pageNumber >= pdf.numPages) return false;
                pageNumber++;
                await render();
                return true;
            },
            zoomOut: async () => {
                if (zoom <= 0.5) return false;
                zoom = Math.max(0.5, zoom - 0.25);
                await render();
                return true;
            },
            zoomIn: async () => {
                if (zoom >= 2) return false;
                zoom = Math.min(2, zoom + 0.25);
                await render();
                return true;
            },
            destroy: () => context.clearRect(0, 0, canvas.width, canvas.height)
        };
    }

    window.ProseginPdfViewer = { mount };
})(window);
