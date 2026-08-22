(function () {
    "use strict";

    let noticeTimer;
    let revealObserver;

    function showNotice(message) {
        const notice = document.getElementById("site-notice");
        if (!notice) return;
        window.clearTimeout(noticeTimer);
        notice.textContent = message;
        notice.classList.add("is-visible");
        noticeTimer = window.setTimeout(() => notice.classList.remove("is-visible"), 2800);
    }

    function updateThemeControl(theme, animate) {
        const button = document.querySelector("[data-theme-toggle]");
        if (!button) return;
        const isDark = theme === "dark";
        button.setAttribute("aria-label", isDark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối");
        if (!animate) return;
        button.classList.remove("is-animating");
        window.requestAnimationFrame(() => button.classList.add("is-animating"));
        window.setTimeout(() => button.classList.remove("is-animating"), 520);
    }

    function setTheme(theme, animate = true) {
        document.documentElement.dataset.theme = theme;
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute("content", theme === "dark" ? "#08101f" : "#eef3fb");
        updateThemeControl(theme, animate);
        if (window.melrandiaThemeStorage) {
            window.melrandiaThemeStorage.write(theme);
        } else {
            try { window.localStorage.setItem("melrandia-theme", theme); } catch { }
            document.cookie = `melrandia-theme=${theme}; Max-Age=31536000; Path=/; SameSite=Lax`;
        }
    }

    function restoreSavedTheme() {
        const savedTheme = window.melrandiaThemeStorage?.read();
        const currentTheme = savedTheme || document.documentElement.dataset.theme || "light";
        document.documentElement.dataset.theme = currentTheme;
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute("content", currentTheme === "dark" ? "#08101f" : "#eef3fb");
        updateThemeControl(currentTheme, false);
    }

    function closeDropdowns(except) {
        document.querySelectorAll("[data-nav-dropdown].is-open").forEach(dropdown => {
            if (dropdown === except) return;
            dropdown.classList.remove("is-open");
            dropdown.querySelector("[data-dropdown-toggle]")?.setAttribute("aria-expanded", "false");
        });
        document.querySelectorAll("[data-language-picker].is-open").forEach(picker => {
            if (picker === except) return;
            picker.classList.remove("is-open");
            picker.querySelector("[data-language-toggle]")?.setAttribute("aria-expanded", "false");
        });
    }

    function closeMobileMenu() {
        const button = document.querySelector("[data-menu-toggle]");
        const menu = document.querySelector("[data-mobile-nav]");
        if (!button || !menu) return;
        button.setAttribute("aria-expanded", "false");
        menu.classList.remove("is-open");
        document.body.classList.remove("menu-open");
    }

    function initializePage() {
        closeMobileMenu();
        closeDropdowns();
        restoreSavedTheme();

        const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        const elements = document.querySelectorAll(".reveal:not(.is-visible)");
        if (reducedMotion || !("IntersectionObserver" in window)) {
            elements.forEach(element => element.classList.add("is-visible"));
            return;
        }
        if (revealObserver) revealObserver.disconnect();
        revealObserver = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add("is-visible");
                revealObserver.unobserve(entry.target);
            });
        }, { threshold: 0.12, rootMargin: "0px 0px -36px" });
        elements.forEach(element => revealObserver.observe(element));
    }

    document.addEventListener("click", event => {
        const themeButton = event.target.closest("[data-theme-toggle]");
        if (themeButton) {
            setTheme(document.documentElement.dataset.theme === "dark" ? "light" : "dark");
            closeDropdowns();
            return;
        }

        const dropdownButton = event.target.closest("[data-dropdown-toggle]");
        if (dropdownButton) {
            const dropdown = dropdownButton.closest("[data-nav-dropdown]");
            if (!dropdown) return;
            const opening = !dropdown.classList.contains("is-open");
            closeDropdowns(dropdown);
            dropdown.classList.toggle("is-open", opening);
            dropdownButton.setAttribute("aria-expanded", opening ? "true" : "false");
            return;
        }

        const languageButton = event.target.closest("[data-language-toggle]");
        if (languageButton) {
            const picker = languageButton.closest("[data-language-picker]");
            if (!picker) return;
            const opening = !picker.classList.contains("is-open");
            closeDropdowns(picker);
            picker.classList.toggle("is-open", opening);
            languageButton.setAttribute("aria-expanded", opening ? "true" : "false");
            return;
        }

        if (event.target.closest("[data-language-en]")) {
            closeDropdowns();
            showNotice("Phiên bản tiếng Anh đang được chuẩn bị.");
            return;
        }
        if (event.target.closest("[data-language-vi]")) {
            closeDropdowns();
            showNotice("Tiếng Việt đang là ngôn ngữ hiển thị.");
            return;
        }

        const menuButton = event.target.closest("[data-menu-toggle]");
        if (menuButton) {
            const menu = document.querySelector("[data-mobile-nav]");
            if (!menu) return;
            const opening = menuButton.getAttribute("aria-expanded") !== "true";
            menuButton.setAttribute("aria-expanded", opening ? "true" : "false");
            menu.classList.toggle("is-open", opening);
            document.body.classList.toggle("menu-open", opening);
            closeDropdowns();
            return;
        }

        if (!event.target.closest("[data-nav-dropdown], [data-language-picker]")) closeDropdowns();
        if (event.target.closest("[data-mobile-nav] a")) closeMobileMenu();
    });

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        closeDropdowns();
        closeMobileMenu();
    });
    window.addEventListener("scroll", () => document.querySelector("[data-site-header]")?.classList.toggle("is-scrolled", window.scrollY > 12), { passive: true });
    window.addEventListener("pageshow", restoreSavedTheme);
    window.addEventListener("storage", event => { if (event.key === "melrandia-theme") restoreSavedTheme(); });
    document.addEventListener("DOMContentLoaded", initializePage);
    if (window.Blazor) window.Blazor.addEventListener("enhancedload", initializePage);
})();
