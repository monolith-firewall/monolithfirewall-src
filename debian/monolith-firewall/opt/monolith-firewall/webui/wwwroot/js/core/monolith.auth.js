/**
 * MonolithFireWall Authentication
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Auth = {
    currentUser: null,

    /**
     * Initialize auth
     */
    init: async function() {
        return await this.checkAuth();
    },

    /**
     * Check authentication status
     */
    checkAuth: async function() {
        try {
            const response = await Monolith.API.get('/user/current');
            if (response.success && response.data) {
                this.currentUser = response.data;
                return true;
            }
            // Not authenticated - response.success is false
            this.currentUser = null;
            return false;
        } catch (error) {
            // Network error or other issue
            console.warn('Auth check failed:', error.message);
            this.currentUser = null;
            return false;
        }
    },

    /**
     * Login
     */
    login: async function(username, password) {
        try {
            const response = await Monolith.API.post('/auth/login', {
                username: username,
                password: password
            });
            
            if (response.success) {
                this.currentUser = response.data.user;
                return true;
            }
            return false;
        } catch (error) {
            console.error('Login error:', error);
            return false;
        }
    },

    /**
     * Logout
     */
    logout: async function() {
        try {
            // Clear session on server (if endpoint exists)
            await Monolith.API.post('/auth/logout', {});
        } catch (error) {
            // Ignore errors
        }
        this.currentUser = null;
        window.location.href = '/login';
    },

    /**
     * Check permission
     */
    hasPermission: function(permissions) {
        if (!permissions || permissions.length === 0) {
            return true;
        }
        
        if (!this.currentUser) {
            return false;
        }
        
        const userPerms = this.currentUser.permissions || [];
        return permissions.some(perm => 
            userPerms.includes(perm) || 
            userPerms.includes('*')
        );
    }
};
