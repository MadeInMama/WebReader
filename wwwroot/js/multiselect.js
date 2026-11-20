(function () {
    const VIEWPORT_PADDING = 8;
    const MENU_GAP = 6;

    function positionMenu(dropdown) {
        const toggle = dropdown.querySelector('.ms-toggle');
        const menu = dropdown.querySelector('.ms-menu');
        const preferUp = dropdown.classList.contains('ms-up');

        const toggleRect = toggle.getBoundingClientRect();
        const viewportW = document.documentElement.clientWidth;
        const viewportH = document.documentElement.clientHeight;

        menu.style.maxHeight = '26em';
        menu.style.left = '0px';
        menu.style.top = '0px';

        const menuRect = menu.getBoundingClientRect();

        let left = toggleRect.left;
        if (left + menuRect.width > viewportW - VIEWPORT_PADDING) {
            left = viewportW - menuRect.width - VIEWPORT_PADDING;
        }
        if (left < VIEWPORT_PADDING) {
            left = VIEWPORT_PADDING;
        }

        const spaceAbove = toggleRect.top - VIEWPORT_PADDING - MENU_GAP;
        const spaceBelow = viewportH - toggleRect.bottom - VIEWPORT_PADDING - MENU_GAP;

        let openUp = preferUp;
        if (preferUp && spaceAbove < Math.min(menuRect.height, 180) && spaceBelow > spaceAbove) {
            openUp = false;
        } else if (!preferUp && spaceBelow < Math.min(menuRect.height, 180) && spaceAbove > spaceBelow) {
            openUp = true;
        }

        let top;
        if (openUp) {
            const maxH = Math.max(120, spaceAbove);
            menu.style.maxHeight = `${maxH}px`;
            const finalHeight = menu.getBoundingClientRect().height;
            top = Math.max(VIEWPORT_PADDING, toggleRect.top - finalHeight - MENU_GAP);
        } else {
            const maxH = Math.max(120, spaceBelow);
            menu.style.maxHeight = `${maxH}px`;
            top = toggleRect.bottom + MENU_GAP;
        }

        menu.style.left = `${left}px`;
        menu.style.top = `${top}px`;
    }

    function updateDropdownLabel(dropdown) {
        const textEl = dropdown.querySelector('.ms-text');
        const defaultText = dropdown.dataset.default;
        const checked = dropdown.querySelectorAll('input[type="checkbox"]:checked');

        if (checked.length === 0) {
            textEl.textContent = defaultText;
        } else if (checked.length === 1) {
            textEl.textContent = checked[0].closest('.ms-item').querySelector('.ms-item-label').textContent;
        } else {
            textEl.textContent = `${defaultText} (${checked.length})`;
        }

        dropdown.dispatchEvent(new CustomEvent('multiselect:change', {
            bubbles: true,
            detail: {
                id: dropdown.id,
                values: Array.from(checked).map(cb => cb.value)
            }
        }));
    }

    document.addEventListener('click', (e) => {
        const toggle = e.target.closest('.ms-toggle');
        if (toggle) {
            const current = toggle.closest('.ms-dropdown');
            const isOpening = !current.classList.contains('open');

            document.querySelectorAll('.ms-dropdown.open').forEach(d => d.classList.remove('open'));

            if (isOpening) {
                current.classList.add('open');
                positionMenu(current);
            }
            return;
        }

        const clearBtn = e.target.closest('.ms-clear');
        if (clearBtn) {
            const dropdown = clearBtn.closest('.ms-dropdown');
            dropdown.querySelectorAll('input[type="checkbox"]').forEach(cb => cb.checked = false);
            updateDropdownLabel(dropdown);
            positionMenu(dropdown);
            return;
        }

        const item = e.target.closest('.ms-item');
        if (item) {
            e.preventDefault();
            const cb = item.querySelector('input[type="checkbox"]');
            cb.checked = !cb.checked;
            updateDropdownLabel(item.closest('.ms-dropdown'));
            return;
        }

        if (!e.target.closest('.ms-dropdown')) {
            document.querySelectorAll('.ms-dropdown.open').forEach(d => d.classList.remove('open'));
        }
    });

    const repositionOpenMenus = () => {
        document.querySelectorAll('.ms-dropdown.open').forEach(positionMenu);
    };

    window.addEventListener('resize', repositionOpenMenus);
    window.addEventListener('scroll', (e) => {
        if (e.target.classList && e.target.classList.contains('ms-menu')) return;
        repositionOpenMenus();
    }, true);

    window.getMultiSelectValues = function (id) {
        const checked = document.querySelectorAll(`#${id} input[type="checkbox"]:checked`);
        return Array.from(checked).map(cb => cb.value);
    };
})();
