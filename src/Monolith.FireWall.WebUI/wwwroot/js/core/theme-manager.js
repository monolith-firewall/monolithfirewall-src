/**
 * Theme Manager - Handles Bootstrap dark/light theme switching
 * Supports: "light", "dark", and "auto" (system preference)
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Theme = {
    currentTheme: null,
    systemPreference: null,
    mediaQuery: null,

    /**
     * Initialize theme manager
     */
    init: async function() {
        console.log('Initializing Theme Manager...');
        
        // Listen for system preference changes
        this.mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        this.systemPreference = this.mediaQuery.matches ? 'dark' : 'light';
        
        // Listen for system preference changes
        this.mediaQuery.addEventListener('change', (e) => {
            this.systemPreference = e.matches ? 'dark' : 'light';
            if (this.currentTheme === 'auto') {
                this.applyTheme('auto');
            }
        });

        // Load theme from user profile or localStorage
        await this.loadTheme();
    },

    /**
     * Load theme from API or localStorage
     */
    loadTheme: async function() {
        try {
            // Try to get from API (user profile)
            if (Monolith.API) {
                const response = await Monolith.API.get('/users/profile/theme');
                if (response.success && response.data && response.data.theme) {
                    this.currentTheme = response.data.theme;
                    localStorage.setItem('monolith-theme', this.currentTheme);
                    this.applyTheme(this.currentTheme);
                    return;
                }
            }
        } catch (error) {
            console.warn('Failed to load theme from API, using localStorage:', error);
        }

        // Fallback to localStorage
        const savedTheme = localStorage.getItem('monolith-theme');
        if (savedTheme && ['light', 'dark', 'auto'].includes(savedTheme)) {
            this.currentTheme = savedTheme;
        } else {
            this.currentTheme = 'dark'; // Default
        }

        this.applyTheme(this.currentTheme);
    },

    /**
     * Get current theme
     */
    getTheme: function() {
        return this.currentTheme || 'dark';
    },

    /**
     * Set and save theme
     */
    setTheme: async function(theme) {
        if (!['light', 'dark', 'auto'].includes(theme)) {
            console.error('Invalid theme:', theme);
            return false;
        }

        this.currentTheme = theme;
        this.applyTheme(theme);

        // Save to localStorage immediately
        localStorage.setItem('monolith-theme', theme);

        // Save to user profile via API
        try {
            if (Monolith.API) {
                const response = await Monolith.API.put('/users/profile/theme', { theme });
                if (response.success) {
                    console.log('Theme saved to user profile');
                } else {
                    console.warn('Failed to save theme to profile:', response.error);
                }
            }
        } catch (error) {
            console.warn('Error saving theme to profile:', error);
            // Continue anyway - localStorage is saved
        }

        return true;
    },

    /**
     * Apply theme to document
     */
    applyTheme: function(theme) {
        let actualTheme = theme;

        // If auto, use system preference
        if (theme === 'auto') {
            actualTheme = this.systemPreference || 'dark';
        }

        // Apply to html element
        const html = document.documentElement;
        html.setAttribute('data-bs-theme', actualTheme);
        
        // Also set a class for custom CSS
        html.classList.remove('theme-light', 'theme-dark', 'theme-auto');
        html.classList.add(`theme-${theme}`);

        console.log(`Theme applied: ${theme} (actual: ${actualTheme})`);
        
        // Dispatch event for other components
        document.dispatchEvent(new CustomEvent('themechange', { 
            detail: { theme, actualTheme } 
        }));
    },

    /**
     * Toggle between light and dark (skips auto)
     */
    toggle: async function() {
        const current = this.getTheme();
        if (current === 'light') {
            await this.setTheme('dark');
        } else if (current === 'dark') {
            await this.setTheme('light');
        } else {
            // If auto, toggle to opposite of current system preference
            await this.setTheme(this.systemPreference === 'dark' ? 'light' : 'dark');
        }
    }
};

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => Monolith.Theme.init());
} else {
    Monolith.Theme.init();
}
