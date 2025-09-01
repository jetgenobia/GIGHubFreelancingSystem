// Global search functionality
class SearchManager {
    constructor() {
        this.initializeEventListeners();
    }

    initializeEventListeners() {
        // Handle search form submission from navigation bars
        const navSearchForms = document.querySelectorAll('form[data-search-form="true"]');
        navSearchForms.forEach(form => {
            form.addEventListener('submit', this.handleNavSearch.bind(this));
        });
    }

    handleNavSearch(event) {
        event.preventDefault();
        
        const form = event.target;
        const searchInput = form.querySelector('input[type="search"]');
        const searchTerm = searchInput.value.trim();
        
        if (searchTerm) {
            this.performSearch(searchTerm, 'all');
        }
    }

    performSearch(searchTerm, searchType = 'all') {
        if (!searchTerm || !searchTerm.trim()) {
            return;
        }

        const searchUrl = new URL('/Search/Search', window.location.origin);
        searchUrl.searchParams.set('searchTerm', searchTerm.trim());
        searchUrl.searchParams.set('searchType', searchType);

        window.location.href = searchUrl.toString();
    }

    // Static method for global access
    static performGlobalSearch(searchTerm, searchType = 'all') {
        if (!searchTerm || !searchTerm.trim()) {
            return;
        }

        const searchUrl = new URL('/Search/Search', window.location.origin);
        searchUrl.searchParams.set('searchTerm', searchTerm.trim());
        searchUrl.searchParams.set('searchType', searchType);

        window.location.href = searchUrl.toString();
    }
}

// Initialize search manager when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    new SearchManager();
});

// Global search function that can be called from any page
function performSearch(searchTerm, searchType = 'all') {
    SearchManager.performGlobalSearch(searchTerm, searchType);
}
