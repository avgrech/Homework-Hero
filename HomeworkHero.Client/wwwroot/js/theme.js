window.themeStore = {
    getTheme: () => {
        const stored = localStorage.getItem('hh-theme');
        if (stored === 'dark' || stored === 'light') {
            return stored;
        }

        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
            ? 'dark'
            : 'light';
    },
    setTheme: (theme) => {
        const next = theme === 'dark' ? 'dark' : 'light';
        localStorage.setItem('hh-theme', next);
        return window.themeStore.applyTheme(next);
    },
    applyTheme: (theme) => {
        const body = document.body;
        const themeClass = theme === 'dark' ? 'theme-dark' : 'theme-light';
        body.classList.remove('theme-dark', 'theme-light');
        body.classList.add(themeClass);
        body.dataset.theme = themeClass;
        return theme;
    },
    init: () => {
        const theme = window.themeStore.getTheme();
        window.themeStore.applyTheme(theme);
        return theme;
    }
};

document.addEventListener('DOMContentLoaded', () => {
    window.themeStore.init();
});
