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
        closeMobileGroups();
    }

    // Keep only one mobile navigation group expanded at a time.
    function closeMobileGroups(except) {
        document.querySelectorAll("[data-mobile-nav-group].is-open").forEach(group => {
            if (group === except) return;
            group.classList.remove("is-open");
            group.querySelector("[data-mobile-nav-group-toggle]")?.setAttribute("aria-expanded", "false");
        });
    }

    function openProjectModal() {
        const dialog = document.querySelector("[data-project-modal]");
        if (dialog && !dialog.open) dialog.showModal();
    }

    function toggleSidebar() {
        const shell = document.querySelector(".management-shell");
        if (!shell) return;
        const isCollapsed = shell.classList.toggle("is-collapsed");
        try { localStorage.setItem("melrandia-sidebar-collapsed", isCollapsed ? "true" : "false"); } catch { }
    }

    function restoreSidebarState() {
        const shell = document.querySelector(".management-shell");
        if (!shell) return;
        try {
            if (localStorage.getItem("melrandia-sidebar-collapsed") === "true") {
                shell.classList.add("is-collapsed");
            }
        } catch { }
    }

    function applyArticleMarkdown(editor, action, value = "") {
        if (!editor) return;
        const source = editor.value;
        const start = editor.selectionStart;
        const end = editor.selectionEnd;
        const selected = source.slice(start, end);
        let replacement = selected;
        let selectionStart = start;
        let selectionEnd = end;

        const wrap = (prefix, suffix = prefix, fallback = "văn bản") => {
            const content = selected || fallback;
            replacement = `${prefix}${content}${suffix}`;
            selectionStart = start + prefix.length;
            selectionEnd = selectionStart + content.length;
        };

        const prefixLines = prefix => {
            replacement = (selected || "văn bản").split("\n").map(line => `${prefix}${line}`).join("\n");
            selectionStart = start;
            selectionEnd = start + replacement.length;
        };

        switch (action) {
            case "h1": prefixLines("# "); break;
            case "h2": prefixLines("## "); break;
            case "h3": prefixLines("### "); break;
            case "quote": prefixLines("> "); break;
            case "bold": wrap("**", "**"); break;
            case "italic": wrap("*", "*"); break;
            case "unordered-list": prefixLines("- "); break;
            case "ordered-list": {
                replacement = (selected || "văn bản").split("\n").map((line, index) => `${index + 1}. ${line}`).join("\n");
                selectionStart = start;
                selectionEnd = start + replacement.length;
                break;
            }
            case "link": {
                const url = window.prompt("Nhập đường dẫn liên kết", "https://");
                if (!url) return;
                const text = selected || "văn bản liên kết";
                replacement = `[${text}](${url.trim()})`;
                selectionStart = start + 1;
                selectionEnd = selectionStart + text.length;
                break;
            }
            case "code": wrap("```\n", "\n```", "mã nguồn"); break;
            case "table": {
                replacement = "| Tiêu đề 1 | Tiêu đề 2 |\n| --- | --- |\n| Nội dung | Nội dung |";
                selectionStart = start;
                selectionEnd = start + replacement.length;
                break;
            }
            case "image": {
                const url = value || window.prompt("Nhập URL ảnh", "https://");
                if (!url) return;
                replacement = `![mô tả ảnh](${url.trim()})`;
                selectionStart = start + 2;
                selectionEnd = selectionStart + "mô tả ảnh".length;
                break;
            }
            default: return;
        }

        editor.setRangeText(replacement, start, end, "end");
        editor.dispatchEvent(new Event("input", { bubbles: true }));
        editor.focus();
        editor.setSelectionRange(selectionStart, selectionEnd);
    }

    function initializePage() {
        closeMobileMenu();
        closeDropdowns();
        restoreSavedTheme();
        restoreSidebarState();
        if (window.location.hash === "#new-project") openProjectModal();
        if (window.location.hash) {
            const target = document.getElementById(window.location.hash.slice(1));
            if (target?.matches("details")) target.open = true;
        }

        const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        const elements = document.querySelectorAll(".reveal:not(.is-visible)");
        if (reducedMotion || !("IntersectionObserver" in window)) {
            elements.forEach(element => element.classList.add("is-visible"));
            return;
        }

        if (revealObserver) revealObserver.disconnect();
        revealObserver = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add("is-visible");
                    revealObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.08, rootMargin: "0px 0px -20px" });

        elements.forEach(element => {
            const rect = element.getBoundingClientRect();
            if (rect.top < (window.innerHeight || document.documentElement.clientHeight) - 20 && rect.bottom > 0) {
                element.classList.add("is-visible");
            } else {
                revealObserver.observe(element);
            }
        });

        if (!window.revealMutationObserver && "MutationObserver" in window) {
            window.revealMutationObserver = new MutationObserver(() => {
                const pending = document.querySelectorAll(".reveal:not(.is-visible)");
                if (pending.length && revealObserver) {
                    pending.forEach(el => {
                        const rect = el.getBoundingClientRect();
                        if (rect.top < (window.innerHeight || document.documentElement.clientHeight) - 20 && rect.bottom > 0) {
                            el.classList.add("is-visible");
                        } else {
                            revealObserver.observe(el);
                        }
                    });
                }
            });
            window.revealMutationObserver.observe(document.body, { childList: true, subtree: true });
        }

        applyManagementFilters();
    }

    // Search and status selects share one filter function so changing one control never
    // accidentally resets the other. Items opt in through data-search-item/data-status.
    function applyManagementFilters() {
        const term = (document.querySelector("[data-management-search]")?.value || "").trim().toLocaleLowerCase("vi");
        const statuses = Array.from(document.querySelectorAll("[data-management-status-filter]"))
            .map(select => select.value).filter(value => value && value !== "all");
        document.querySelectorAll("[data-search-item], [data-status]").forEach(item => {
            const searchableText = (item.dataset.searchItem || item.textContent || "").toLocaleLowerCase("vi");
            const matchesText = term.length === 0 || searchableText.includes(term);
            const matchesStatus = statuses.length === 0 || statuses.includes(item.dataset.status || "");
            item.hidden = !matchesText || !matchesStatus;
        });
    }

    document.addEventListener("click", event => {
        const formatButton = event.target.closest("[data-article-format]");
        if (formatButton) {
            applyArticleMarkdown(document.getElementById(formatButton.dataset.editorTarget), formatButton.dataset.articleFormat);
            return;
        }

        const imageButton = event.target.closest("[data-article-image-insert]");
        if (imageButton) {
            const editor = imageButton.closest("form")?.querySelector("#article-body");
            if (editor && imageButton.dataset.imageUrl)
                applyArticleMarkdown(editor, "image", imageButton.dataset.imageUrl);
            return;
        }

        const focusArticleImages = event.target.closest("[data-article-focus]");
        if (focusArticleImages) {
            document.getElementById(focusArticleImages.dataset.articleFocus)?.scrollIntoView({ behavior: "smooth", block: "center" });
            return;
        }

        const projectModalOpen = event.target.closest("[data-project-modal-open]");
        if (projectModalOpen) {
            openProjectModal();
            return;
        }

        const projectModalClose = event.target.closest("[data-project-modal-close]");
        if (projectModalClose) {
            projectModalClose.closest("[data-project-modal]")?.close();
            return;
        }

        const projectModal = event.target.closest("[data-project-modal]");
        if (projectModal && event.target === projectModal) projectModal.close();

        const passwordButton = event.target.closest("[data-password-toggle]");
        if (passwordButton) {
            const input = document.getElementById(passwordButton.dataset.passwordToggle);
            if (!input) return;
            const reveal = input.type === "password";
            input.type = reveal ? "text" : "password";
            passwordButton.setAttribute("aria-label", reveal ? "Ẩn mật khẩu" : "Hiện mật khẩu");
            passwordButton.setAttribute("aria-pressed", reveal ? "true" : "false");
            return;
        }

        const sidebarToggleBtn = event.target.closest("[data-sidebar-toggle]");
        if (sidebarToggleBtn) {
            toggleSidebar();
            return;
        }

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

        // Mobile project and article groups use delegated click handling.
        const mobileGroupButton = event.target.closest("[data-mobile-nav-group-toggle]");
        if (mobileGroupButton) {
            const group = mobileGroupButton.closest("[data-mobile-nav-group]");
            if (!group) return;
            const opening = !group.classList.contains("is-open");
            closeMobileGroups(group);
            group.classList.toggle("is-open", opening);
            mobileGroupButton.setAttribute("aria-expanded", opening ? "true" : "false");
            mobileGroupButton.setAttribute("aria-label", opening ? "Đóng danh sách" : "Mở danh sách");
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
    document.addEventListener("input", event => {
        if (event.target.matches("[data-article-title]")) {
            const slug = document.querySelector("[data-article-slug]");
            if (slug && (!slug.value || slug.dataset.autoGenerated === "true")) {
                slug.value = event.target.value.normalize("NFD").replace(/[\u0300-\u036f]/g, "")
                    .replace(/đ/g, "d").replace(/Đ/g, "D").toLocaleLowerCase("vi")
                    .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 160);
                slug.dataset.autoGenerated = "true";
            }
        }
        if (!event.target.matches("[data-management-search]")) return;
        applyManagementFilters();
    });
    document.addEventListener("input", event => {
        if (event.target.matches("[data-article-slug]") && !event.isTrusted) return;
        if (event.target.matches("[data-article-slug]")) event.target.dataset.autoGenerated = "false";
    });
    document.addEventListener("change", event => {
        const headingSelect = event.target.closest("[data-article-heading]");
        if (headingSelect?.value) {
            applyArticleMarkdown(document.getElementById(headingSelect.dataset.articleHeading), headingSelect.value);
            headingSelect.value = "";
        }

        const coverInput = event.target.closest("[data-article-cover-input]");
        if (coverInput) {
            const file = coverInput.files?.[0];
            const container = coverInput.closest("form")?.querySelector("[data-article-cover-preview-container]");
            const image = container?.querySelector("[data-article-cover-preview]");
            if (file && container && image) {
                const reader = new FileReader();
                reader.addEventListener("load", () => {
                    image.src = String(reader.result || "");
                    container.hidden = false;
                }, { once: true });
                reader.readAsDataURL(file);
            }
        }

        if (!event.target.matches("[data-management-status-filter]")) return;
        applyManagementFilters();
    });
    document.addEventListener("click", event => {
        const clearCover = event.target.closest("[data-article-cover-clear]");
        if (clearCover) {
            const container = clearCover.closest("[data-article-cover-preview-container]");
            const form = clearCover.closest("form");
            const input = form?.querySelector("[data-article-cover-input]");
            const image = container?.querySelector("[data-article-cover-preview]");
            if (input) input.value = "";
            if (image) image.removeAttribute("src");
            if (container) container.hidden = true;
            return;
        }

        const copySlug = event.target.closest("[data-article-copy-slug]");
        if (copySlug) {
            const form = copySlug.closest("form") || document;
            const slugInput = form.querySelector("[data-article-slug]");
            const slug = (slugInput?.value || "bai-viet").trim();
            const fullUrl = `${window.location.origin}/bai-viet/${slug}`;
            navigator.clipboard.writeText(fullUrl).then(() => {
                copySlug.classList.add("copied");
                const oldContent = copySlug.innerHTML;
                copySlug.innerHTML = '<i class="bi bi-check2"></i>';
                showNotice("Đã sao chép liên kết bài viết!");
                setTimeout(() => {
                    copySlug.classList.remove("copied");
                    copySlug.innerHTML = oldContent;
                }, 2000);
            }).catch(() => {
                showNotice("Đã sao chép: " + fullUrl);
            });
            return;
        }

        const dropzone = event.target.closest("[data-article-cover-dropzone]");
        if (dropzone && !event.target.closest("[data-article-cover-clear], input, button")) {
            const form = dropzone.closest("form");
            form?.querySelector("[data-article-cover-input]")?.click();
            return;
        }

        const presetToggle = event.target.closest("[data-article-preset-toggle]");
        if (presetToggle) {
            const presets = document.getElementById("article-image-presets");
            if (presets) presets.hidden = !presets.hidden;
            return;
        }

        const presetCover = event.target.closest("[data-article-preset-cover]");
        if (presetCover) {
            const form = presetCover.closest("form");
            const url = presetCover.dataset.imageUrl;
            const container = form?.querySelector("[data-article-cover-preview-container]");
            const image = container?.querySelector("[data-article-cover-preview]");
            if (url && container && image) {
                image.src = url;
                container.hidden = false;
                const altInput = form.querySelector("input[name='coverAlt']");
                if (altInput && !altInput.value) altInput.value = presetCover.dataset.imageAlt || "";
                showNotice("Đã chọn ảnh đại diện từ mẫu có sẵn");
            }
            return;
        }

        const saveDraft = event.target.closest("[data-article-save-draft]");
        if (saveDraft) {
            const form = saveDraft.closest("form");
            if (form) {
                const draftRadio = form.querySelector("input[name='status'][value='Draft']");
                if (draftRadio) draftRadio.checked = true;
                if (form.reportValidity()) {
                    form.submit();
                }
            }
            return;
        }
    });

    document.addEventListener("dragover", event => {
        const dropzone = event.target.closest("[data-article-cover-dropzone]");
        if (dropzone) {
            event.preventDefault();
            dropzone.classList.add("is-dragover");
        }
    });

    document.addEventListener("dragleave", event => {
        const dropzone = event.target.closest("[data-article-cover-dropzone]");
        if (dropzone) dropzone.classList.remove("is-dragover");
    });

    document.addEventListener("drop", event => {
        const dropzone = event.target.closest("[data-article-cover-dropzone]");
        if (dropzone) {
            event.preventDefault();
            dropzone.classList.remove("is-dragover");
            const file = event.dataTransfer?.files?.[0];
            if (file) {
                const form = dropzone.closest("form");
                const input = form?.querySelector("[data-article-cover-input]");
                const container = form?.querySelector("[data-article-cover-preview-container]");
                const image = container?.querySelector("[data-article-cover-preview]");
                if (input && container && image) {
                    try {
                        const dataTransfer = new DataTransfer();
                        dataTransfer.items.add(file);
                        input.files = dataTransfer.files;
                    } catch { }
                    const reader = new FileReader();
                    reader.addEventListener("load", () => {
                        image.src = String(reader.result || "");
                        container.hidden = false;
                    }, { once: true });
                    reader.readAsDataURL(file);
                }
            }
        }
    });
    window.addEventListener("scroll", () => document.querySelector("[data-site-header]")?.classList.toggle("is-scrolled", window.scrollY > 12), { passive: true });
    window.addEventListener("pageshow", restoreSavedTheme);
    window.addEventListener("storage", event => { if (event.key === "melrandia-theme") restoreSavedTheme(); });
    document.addEventListener("DOMContentLoaded", initializePage);
    if (window.Blazor) window.Blazor.addEventListener("enhancedload", initializePage);
})();
