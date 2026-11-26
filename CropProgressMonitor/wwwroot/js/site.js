// Global loading spinner for API-intensive actions and navigation
(function () {
    const overlay = document.getElementById('globalLoadingOverlay');

    window.showAgriLoader = function (message) {
        if (!overlay) return;
        const msgEl = document.getElementById('globalLoadingMessage');
        if (msgEl && message) {
            msgEl.textContent = message;
        }
        overlay.classList.add('active');
    };

    window.hideAgriLoader = function () {
        if (!overlay) return;
        overlay.classList.remove('active');
    };

    document.addEventListener('DOMContentLoaded', function () {
        // Animate progress bars on load
        document.querySelectorAll('.agri-progress-bar[data-target-width]').forEach(function (bar) {
            const target = bar.getAttribute('data-target-width');
            requestAnimationFrame(function () {
                bar.style.width = target + '%';
            });
        });

        // Show loader when clicking links or submitting forms marked data-show-loader
        document.querySelectorAll('[data-show-loader]').forEach(function (el) {
            const msg = el.getAttribute('data-loader-text') || 'Fetching live weather & computing GDD...';
            if (el.tagName === 'FORM') {
                el.addEventListener('submit', function () {
                    if (typeof $ !== 'undefined' && $(el).valid && !$(el).valid()) {
                        return;
                    }
                    window.showAgriLoader(msg);
                });
            } else {
                el.addEventListener('click', function () {
                    window.showAgriLoader(msg);
                });
            }
        });
    });
})();
