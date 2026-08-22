(function () {
    "use strict";

    const storageKey = "melrandia-theme";
    const allowedThemes = new Set(["light", "dark"]);

    // Cookie là lớp dự phòng cho những trình duyệt chặn localStorage. Cả hai
    // đều là dữ liệu cục bộ và không chứa thông tin tài khoản người dùng.
    function readCookie() {
        const entry = document.cookie.split("; ").find(item => item.startsWith(`${storageKey}=`));
        return entry ? decodeURIComponent(entry.split("=").slice(1).join("=")) : null;
    }

    function read() {
        try {
            const stored = window.localStorage.getItem(storageKey);
            if (allowedThemes.has(stored)) return stored;
        } catch { }

        const cookieTheme = readCookie();
        return allowedThemes.has(cookieTheme) ? cookieTheme : null;
    }

    function write(theme) {
        if (!allowedThemes.has(theme)) return;
        try { window.localStorage.setItem(storageKey, theme); } catch { }
        document.cookie = `${storageKey}=${encodeURIComponent(theme)}; Max-Age=31536000; Path=/; SameSite=Lax`;
    }

    const savedTheme = read();
    const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    document.documentElement.dataset.theme = savedTheme || (prefersDark ? "dark" : "light");

    // site.js dùng chung helper này để không có hai cách đọc/ghi theme khác nhau.
    window.melrandiaThemeStorage = { read, write };
})();
