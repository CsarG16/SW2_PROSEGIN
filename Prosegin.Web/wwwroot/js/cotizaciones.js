(() => {
    document.querySelectorAll("[data-quote-row-menu]").forEach(button => {
        const dropdown = button.closest(".dropdown");
        const menu = dropdown?.querySelector(".dropdown-menu");
        if (!dropdown || !menu) return;

        button.addEventListener("show.bs.dropdown", () => document.body.append(menu));
        button.addEventListener("hidden.bs.dropdown", () => dropdown.append(menu));
    });

    const togglePanel = id => {
        const target = document.querySelector(`[data-quote-upload-row="${id}"]`);
        if (!target) return;

        const shouldOpen = target.hidden;
        document.querySelectorAll("[data-quote-upload-row]").forEach(row => {
            row.hidden = true;
        });
        if (shouldOpen) {
            target.hidden = false;
            target.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }
    };

    document.querySelectorAll("[data-toggle-quote-panel]").forEach(button => {
        button.addEventListener("click", () => togglePanel(button.dataset.toggleQuotePanel));
    });

    document.querySelectorAll("[data-quote-upload-form]").forEach(form => {
        const input = form.querySelector("[data-pdf-input]");
        const dropZone = form.querySelector("[data-file-trigger]");
        const fileName = form.querySelector("[data-file-name]");
        if (!input || !dropZone || !fileName) return;

        const setFile = file => {
            if (!file) {
                fileName.textContent = "Ningún archivo seleccionado";
                return;
            }
            if (!file.name.toLowerCase().endsWith(".pdf")) {
                input.value = "";
                fileName.textContent = "El archivo debe tener extensión PDF.";
                return;
            }
            if (file.size > 10 * 1024 * 1024) {
                input.value = "";
                fileName.textContent = "El archivo supera el límite de 10 MB.";
                return;
            }
            fileName.textContent = `${file.name} · ${(file.size / 1024 / 1024).toFixed(2)} MB`;
        };

        dropZone.addEventListener("click", () => input.click());
        input.addEventListener("change", () => setFile(input.files[0]));
        dropZone.addEventListener("dragover", event => {
            event.preventDefault();
            dropZone.classList.add("is-dragging");
        });
        dropZone.addEventListener("dragleave", () => dropZone.classList.remove("is-dragging"));
        dropZone.addEventListener("drop", event => {
            event.preventDefault();
            dropZone.classList.remove("is-dragging");
            const file = event.dataTransfer.files[0];
            if (!file) return;

            const transfer = new DataTransfer();
            transfer.items.add(file);
            input.files = transfer.files;
            setFile(file);
        });

        form.querySelector("[data-cancel-quote-panel]")?.addEventListener("click", () => {
            form.reset();
            fileName.textContent = "Ningún archivo seleccionado";
            const panel = form.closest("[data-quote-upload-row]");
            if (panel) panel.hidden = true;
        });
    });
})();
