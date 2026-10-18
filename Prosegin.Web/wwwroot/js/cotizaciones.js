// ==========================================================================
// CLIENT AVATAR & MONOGRAM LOGIC (GOOGLE FAVICON + FALLBACK DETERMINISTA)
// ==========================================================================
window.ClientAvatar = {
    genericEmailProviders: new Set([
        "gmail.com", "googlemail.com",
        "hotmail.com", "hotmail.es",
        "outlook.com", "outlook.es",
        "yahoo.com", "yahoo.es", "ymail.com",
        "live.com", "live.com.pe",
        "icloud.com", "me.com", "mac.com",
        "proton.me", "protonmail.com",
        "msn.com",
        "aol.com",
        "zoho.com",
        "mail.com"
    ]),

    extractCorporateDomain(email) {
        if (!email || typeof email !== "string" || !email.includes("@")) return null;
        const parts = email.split("@");
        if (parts.length !== 2) return null;
        const domain = parts[1].trim().toLowerCase();
        if (!domain || !domain.includes(".") || domain.startsWith(".") || domain.endsWith(".")) return null;
        if (this.genericEmailProviders.has(domain)) return null;
        return domain;
    },

    getCorporateFaviconUrl(email) {
        const domain = this.extractCorporateDomain(email);
        return domain ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=128` : null;
    },

    getInitials(businessName) {
        if (!businessName || typeof businessName !== "string") return "CL";
        const cleaned = businessName
            .replace(/\b(S\.?A\.?C\.?|S\.?R\.?L\.?|S\.?A\.?|E\.?I\.?R\.?L\.?|S\.?A\.?A\.?|SOCIEDAD\s+ANONIMA(\s+CERRADA)?|SOCIEDAD\s+COMERCIAL\s+DE\s+RESPONSABILIDAD\s+LIMITADA|EMPRESA\s+INDIVIDUAL\s+DE\s+RESPONSABILIDAD\s+LIMITADA)\b/gi, "")
            .replace(/[^\w\s]/g, " ");

        const stopWords = new Set(["de", "del", "la", "las", "el", "los", "y", "e", "en", "por", "para"]);
        const words = cleaned
            .split(/\s+/)
            .filter(w => w.length > 0 && !stopWords.has(w.toLowerCase()));

        if (words.length === 0) {
            const fallback = businessName.split(/\s+/).filter(Boolean);
            if (fallback.length > 0 && fallback[0].length >= 2) {
                return fallback[0].substring(0, 2).toUpperCase();
            }
            return "CL";
        }
        if (words.length === 1) {
            return words[0].length >= 2 ? words[0].substring(0, 2).toUpperCase() : words[0].toUpperCase();
        }
        return (words[0][0] + words[1][0]).toUpperCase();
    },

    getColorScheme(identifier) {
        const id = (identifier || "PROSEGIN").toString();
        let hash = 0;
        for (let i = 0; i < id.length; i++) {
            hash = (hash * 31 + id.charCodeAt(i)) & 0x7fffffff;
        }
        const schemes = [
            "avatar-scheme-slate",
            "avatar-scheme-emerald",
            "avatar-scheme-amber",
            "avatar-scheme-violet",
            "avatar-scheme-indigo"
        ];
        return schemes[hash % schemes.length];
    },

    handleAvatarError(img) {
        if (!img) return;
        img.style.display = "none";
        const fallback = img.nextElementSibling;
        if (fallback) {
            fallback.style.display = "flex";
        }
    }
};

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

    const toggleAccordion = id => {
        const body = document.getElementById(`accordion-body-${id}`);
        const chevron = document.getElementById(`chevron-${id}`);
        if (!body) return;

        const isClosed = body.style.display === "none";
        if (isClosed) {
            body.style.display = "";
            if (chevron) chevron.classList.remove("chevron-rotated");
        } else {
            body.style.display = "none";
            if (chevron) chevron.classList.add("chevron-rotated");
        }
    };

    document.querySelectorAll("[data-accordion-toggle]").forEach(button => {
        button.addEventListener("click", () => toggleAccordion(button.dataset.accordionToggle));
        button.addEventListener("keydown", e => {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                toggleAccordion(button.dataset.accordionToggle);
            }
        });
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
