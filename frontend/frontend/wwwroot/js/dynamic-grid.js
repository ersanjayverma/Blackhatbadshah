// Dynamic Grid System - Drag, Drop & Resize
// Uses interact.js for drag and resize functionality

(function() {
    'use strict';

    const GRID_STORAGE_KEY = 'bhb-dashboard-grid-layout';
    const GRID_SIZE = 10; // Snap to 10px grid
    const MIN_WIDTH = 150;
    const MIN_HEIGHT = 100;

    let gridContainer = null;
    let blazorRef = null;
    let isEditMode = false;
    let initialized = false;
    let currentPageId = null;
    let saveToServerEnabled = true;

    // Check if interact.js is loaded
    function checkInteract() {
        if (typeof interact === 'undefined') {
            console.error('interact.js is not loaded! Drag and resize will not work.');
            return false;
        }
        return true;
    }

    // Initialize the dynamic grid system
    window.initDynamicGrid = (dotNetRef, containerId, pageId) => {
        if (!checkInteract()) return;

        blazorRef = dotNetRef;
        gridContainer = document.getElementById(containerId);
        currentPageId = pageId || containerId;

        if (!gridContainer) {
            console.error('Grid container not found:', containerId);
            return;
        }

        // Clean up any previous interact instances
        if (initialized) {
            const items = gridContainer.querySelectorAll('.grid-item');
            items.forEach(item => {
                try { interact(item).unset(); } catch(e) {}
                item.dataset.initialized = 'false';
            });
        }

        // Load saved layout
        loadLayout();

        // Initialize all grid items
        initializeGridItems();

        initialized = true;
        console.log('Dynamic grid initialized with interact.js for page:', currentPageId);
    };

    // Initialize drag and resize for all grid items
    function initializeGridItems() {
        if (!gridContainer) return;

        const items = gridContainer.querySelectorAll('.grid-item');
        items.forEach(item => {
            initializeItem(item);
        });
    }

    // Initialize a single grid item
    function initializeItem(item) {
        // Skip if already initialized
        if (item.dataset.initialized === 'true') return;

        if (!gridContainer) {
            console.error('Grid container not set');
            return;
        }

        item.dataset.initialized = 'true';

        // Store original position info
        const rect = item.getBoundingClientRect();
        const containerRect = gridContainer.getBoundingClientRect();
        item.dataset.originalX = rect.left - containerRect.left;
        item.dataset.originalY = rect.top - containerRect.top;
        item.dataset.originalWidth = rect.width;
        item.dataset.originalHeight = rect.height;

        // Add drag handle if not present
        ensureDragHandle(item);

        // Add resize handles
        addResizeHandles(item);

        // Initialize interact.js for this item
        setupInteract(item);

        console.log('Initialized grid item:', item.dataset.gridId);
    }

    function setupInteract(item) {
        if (!checkInteract()) return;

        // Unset any previous interact instance
        try { interact(item).unset(); } catch(e) {}

        interact(item)
            .draggable({
                allowFrom: '.drag-handle',
                ignoreFrom: '.resize-handle',
                inertia: false,
                modifiers: [
                    interact.modifiers.snap({
                        targets: [
                            interact.snappers.grid({ x: GRID_SIZE, y: GRID_SIZE })
                        ],
                        range: Infinity,
                        relativePoints: [{ x: 0, y: 0 }]
                    })
                ],
                autoScroll: true,
                enabled: true,
                listeners: {
                    start(event) {
                        if (!isEditMode) {
                            event.interaction.stop();
                            return;
                        }
                        const target = event.target;
                        target.classList.add('dragging');

                        // Get current position
                        const rect = target.getBoundingClientRect();
                        const containerRect = gridContainer.getBoundingClientRect();

                        // Calculate position relative to container
                        const currentX = parseFloat(target.dataset.x) || (rect.left - containerRect.left);
                        const currentY = parseFloat(target.dataset.y) || (rect.top - containerRect.top);

                        // Store dimensions
                        target.dataset.x = currentX;
                        target.dataset.y = currentY;

                        // Make position absolute for free-form dragging
                        // Use !important via cssText to override Bootstrap
                        target.style.cssText = `
                            position: absolute !important;
                            z-index: 1000 !important;
                            width: ${rect.width}px !important;
                            height: ${rect.height}px !important;
                            left: ${currentX}px !important;
                            top: ${currentY}px !important;
                            transform: none !important;
                            margin: 0 !important;
                            max-width: none !important;
                            flex: none !important;
                        `;
                    },
                    move(event) {
                        if (!isEditMode) return;
                        const target = event.target;
                        const x = (parseFloat(target.dataset.x) || 0) + event.dx;
                        const y = (parseFloat(target.dataset.y) || 0) + event.dy;

                        target.style.left = x + 'px';
                        target.style.top = y + 'px';
                        target.dataset.x = x;
                        target.dataset.y = y;
                    },
                    end(event) {
                        const target = event.target;
                        target.classList.remove('dragging');
                        target.style.zIndex = '';
                        saveLayout();
                        notifyBlazor('layoutChanged');
                    }
                }
            })
            .resizable({
                edges: { left: '.resize-handle.left, .resize-handle.top-left, .resize-handle.bottom-left',
                         right: '.resize-handle.right, .resize-handle.top-right, .resize-handle.bottom-right',
                         bottom: '.resize-handle.bottom, .resize-handle.bottom-left, .resize-handle.bottom-right',
                         top: '.resize-handle.top, .resize-handle.top-left, .resize-handle.top-right' },
                enabled: true,
                listeners: {
                    start(event) {
                        if (!isEditMode) {
                            event.interaction.stop();
                            return;
                        }
                        const target = event.target;
                        target.classList.add('resizing');

                        // Get current position
                        const rect = target.getBoundingClientRect();
                        const containerRect = gridContainer.getBoundingClientRect();

                        // Calculate position if not set
                        if (!target.dataset.x || target.style.position !== 'absolute') {
                            target.dataset.x = rect.left - containerRect.left;
                            target.dataset.y = rect.top - containerRect.top;
                        }

                        // Make position absolute with !important
                        target.style.cssText = `
                            position: absolute !important;
                            z-index: 100 !important;
                            width: ${rect.width}px !important;
                            height: ${rect.height}px !important;
                            left: ${target.dataset.x}px !important;
                            top: ${target.dataset.y}px !important;
                            transform: none !important;
                            margin: 0 !important;
                            max-width: none !important;
                            flex: none !important;
                        `;
                    },
                    move(event) {
                        if (!isEditMode) return;
                        const target = event.target;
                        let x = parseFloat(target.dataset.x) || 0;
                        let y = parseFloat(target.dataset.y) || 0;

                        // Update width and height with !important
                        target.style.width = event.rect.width + 'px';
                        target.style.height = event.rect.height + 'px';

                        // Translate when resizing from top or left edges
                        x += event.deltaRect.left;
                        y += event.deltaRect.top;

                        target.style.left = x + 'px';
                        target.style.top = y + 'px';
                        target.dataset.x = x;
                        target.dataset.y = y;
                    },
                    end(event) {
                        event.target.classList.remove('resizing');
                        saveLayout();
                        notifyBlazor('layoutChanged');

                        // Trigger chart resize if present
                        setTimeout(() => resizeCharts(event.target), 100);
                    }
                },
                modifiers: [
                    interact.modifiers.restrictSize({
                        min: { width: MIN_WIDTH, height: MIN_HEIGHT }
                    }),
                    interact.modifiers.snap({
                        targets: [
                            interact.snappers.grid({ x: GRID_SIZE, y: GRID_SIZE })
                        ],
                        range: Infinity,
                        relativePoints: [{ x: 0, y: 0 }]
                    })
                ],
                inertia: false
            });

        console.log('Interact setup for:', item.dataset.gridId);
    }

    // Add resize handles to an item
    function addResizeHandles(item) {
        // Remove existing handles first to prevent duplicates
        item.querySelectorAll('.resize-handle').forEach(h => h.remove());

        const positions = ['top-left', 'top-right', 'bottom-left', 'bottom-right', 'top', 'right', 'bottom', 'left'];

        positions.forEach(pos => {
            const handle = document.createElement('div');
            handle.className = `resize-handle ${pos}`;
            handle.dataset.resizeHandle = pos;
            // Prevent text selection during resize
            handle.addEventListener('selectstart', e => e.preventDefault());
            item.appendChild(handle);
        });
    }

    // Resize charts inside an element
    function resizeCharts(element) {
        const canvases = element.querySelectorAll('canvas');
        canvases.forEach(canvas => {
            const chartId = canvas.id;
            if (chartId && window.dashboardCharts && window.dashboardCharts[chartId]) {
                try {
                    window.dashboardCharts[chartId].resize();
                } catch (e) {
                    console.log('Chart resize skipped:', chartId);
                }
            }
        });
    }

    // Save layout to localStorage and server
    function saveLayout() {
        if (!gridContainer) return;

        const items = gridContainer.querySelectorAll('.grid-item');
        const layout = {};

        items.forEach(item => {
            const id = item.dataset.gridId;
            if (id && item.style.position === 'absolute') {
                layout[id] = {
                    x: parseFloat(item.dataset.x) || 0,
                    y: parseFloat(item.dataset.y) || 0,
                    width: item.offsetWidth,
                    height: item.offsetHeight,
                    isCustom: true
                };
            }
        });

        if (Object.keys(layout).length > 0) {
            const layoutJson = JSON.stringify(layout);

            // Save to localStorage as fallback
            const storageKey = currentPageId ? `${GRID_STORAGE_KEY}-${currentPageId}` : GRID_STORAGE_KEY;
            localStorage.setItem(storageKey, layoutJson);

            // Notify Blazor to save to server
            if (blazorRef && saveToServerEnabled) {
                try {
                    blazorRef.invokeMethodAsync('SaveLayoutToServer', currentPageId, layoutJson);
                } catch (e) {
                    console.log('Server save skipped:', e.message);
                }
            }

            console.log('Layout saved:', layout);
        }
    }

    // Load layout from localStorage
    function loadLayout() {
        const storageKey = currentPageId ? `${GRID_STORAGE_KEY}-${currentPageId}` : GRID_STORAGE_KEY;
        const savedLayout = localStorage.getItem(storageKey);
        if (!savedLayout) return;

        try {
            const layout = JSON.parse(savedLayout);
            // Delay apply to ensure DOM is ready
            setTimeout(() => applyLayout(layout), 100);
            console.log('Layout loaded from localStorage:', layout);
        } catch (e) {
            console.error('Failed to parse saved layout:', e);
        }
    }

    // Apply layout from server (called by Blazor)
    window.applyServerLayout = (layoutJson) => {
        if (!layoutJson || layoutJson === '{}') return;

        try {
            const layout = JSON.parse(layoutJson);
            applyLayout(layout);

            // Also save to localStorage as cache
            const storageKey = currentPageId ? `${GRID_STORAGE_KEY}-${currentPageId}` : GRID_STORAGE_KEY;
            localStorage.setItem(storageKey, layoutJson);

            console.log('Layout applied from server:', layout);
        } catch (e) {
            console.error('Failed to apply server layout:', e);
        }
    };

    // Apply layout to grid items
    function applyLayout(layout) {
        if (!gridContainer) return;

        const items = gridContainer.querySelectorAll('.grid-item');
        items.forEach(item => {
            const id = item.dataset.gridId;
            if (id && layout[id] && layout[id].isCustom) {
                const pos = layout[id];
                // Use cssText to ensure Bootstrap doesn't override
                item.style.cssText = `
                    position: absolute !important;
                    left: ${pos.x}px !important;
                    top: ${pos.y}px !important;
                    width: ${pos.width}px !important;
                    height: ${pos.height}px !important;
                    margin: 0 !important;
                    max-width: none !important;
                    flex: none !important;
                `;
                item.dataset.x = pos.x;
                item.dataset.y = pos.y;
            }
        });

        // Resize charts after applying layout
        setTimeout(() => {
            items.forEach(item => resizeCharts(item));
        }, 200);
    }

    // Reset layout to default
    window.resetGridLayout = () => {
        // Remove from localStorage
        const storageKey = currentPageId ? `${GRID_STORAGE_KEY}-${currentPageId}` : GRID_STORAGE_KEY;
        localStorage.removeItem(storageKey);

        if (!gridContainer) return;

        const items = gridContainer.querySelectorAll('.grid-item');
        items.forEach(item => {
            // Clear all inline styles
            item.style.cssText = '';
            item.dataset.x = '';
            item.dataset.y = '';
        });

        // Notify Blazor to delete from server
        if (blazorRef && currentPageId) {
            try {
                blazorRef.invokeMethodAsync('ResetLayoutOnServer', currentPageId);
            } catch (e) {
                console.log('Server reset skipped:', e.message);
            }
        }

        notifyBlazor('layoutReset');
        console.log('Layout reset');

        // Resize charts
        setTimeout(() => {
            items.forEach(item => resizeCharts(item));
        }, 200);
    };

    // Toggle edit mode
    window.toggleGridEditMode = (enabled) => {
        isEditMode = enabled;

        if (!gridContainer) return;

        if (enabled) {
            gridContainer.classList.add('edit-mode');

            // Pre-calculate positions for all items and convert to absolute positioning
            const items = gridContainer.querySelectorAll('.grid-item');
            const containerRect = gridContainer.getBoundingClientRect();

            items.forEach(item => {
                const rect = item.getBoundingClientRect();

                // Store original position if not already moved
                if (!item.dataset.x || item.style.position !== 'absolute') {
                    item.dataset.x = rect.left - containerRect.left;
                    item.dataset.y = rect.top - containerRect.top;
                    item.dataset.originalWidth = rect.width;
                    item.dataset.originalHeight = rect.height;
                }

                // Ensure drag handle exists
                ensureDragHandle(item);

                // Re-initialize interact if needed
                if (item.dataset.initialized !== 'true') {
                    initializeItem(item);
                }
            });

            console.log('Edit mode enabled - items prepared for dragging');
        } else {
            gridContainer.classList.remove('edit-mode');
        }

        console.log('Edit mode:', enabled);
    };

    // Ensure drag handle exists on an item
    function ensureDragHandle(item) {
        if (item.querySelector('.drag-handle')) return;

        const handle = document.createElement('div');
        handle.className = 'drag-handle';
        handle.innerHTML = '<i class="bi bi-grip-vertical"></i>';

        const cardHeader = item.querySelector('.card-header');
        if (cardHeader) {
            cardHeader.style.display = 'flex';
            cardHeader.style.alignItems = 'center';
            cardHeader.insertBefore(handle, cardHeader.firstChild);
        } else {
            const card = item.querySelector('.card');
            if (card) {
                handle.style.position = 'absolute';
                handle.style.top = '8px';
                handle.style.left = '8px';
                handle.style.zIndex = '5';
                handle.style.background = 'var(--surface-2)';
                card.style.position = 'relative';
                card.insertBefore(handle, card.firstChild);
            }
        }
    }

    // Refresh grid items (called after Blazor re-renders)
    window.refreshDynamicGrid = () => {
        if (!gridContainer) return;

        const items = gridContainer.querySelectorAll('.grid-item');
        items.forEach(item => {
            if (item.dataset.initialized !== 'true') {
                initializeItem(item);
            }
        });

        // Reload layout for new items
        const savedLayout = localStorage.getItem(GRID_STORAGE_KEY);
        if (savedLayout) {
            try {
                applyLayout(JSON.parse(savedLayout));
            } catch (e) { }
        }
    };

    // Notify Blazor of changes
    function notifyBlazor(eventName, data) {
        if (blazorRef) {
            try {
                blazorRef.invokeMethodAsync('OnGridEvent', eventName, data || {});
            } catch (e) {
                console.log('Blazor notification skipped');
            }
        }
    }

    // Export layout as JSON
    window.exportGridLayout = () => {
        const layout = localStorage.getItem(GRID_STORAGE_KEY);
        return layout || '{}';
    };

    // Import layout from JSON
    window.importGridLayout = (layoutJson) => {
        try {
            const layout = JSON.parse(layoutJson);
            localStorage.setItem(GRID_STORAGE_KEY, layoutJson);
            applyLayout(layout);
            return true;
        } catch (e) {
            console.error('Failed to import layout:', e);
            return false;
        }
    };

    // Get current edit mode state
    window.isGridEditMode = () => isEditMode;

    // Quick setup for any page - call this to make a container draggable
    window.setupDynamicGrid = (containerId) => {
        if (!checkInteract()) {
            console.error('interact.js not loaded');
            return false;
        }

        gridContainer = document.getElementById(containerId);
        if (!gridContainer) {
            console.error('Container not found:', containerId);
            return false;
        }

        // Add the container class
        gridContainer.classList.add('dynamic-grid-container');

        // Initialize all grid items in the container
        initializeGridItems();

        // Load any saved layout
        loadLayout();

        console.log('Dynamic grid setup complete for:', containerId);
        return true;
    };

    // Reinitialize a specific item (useful after dynamic content changes)
    window.reinitializeGridItem = (itemElement) => {
        if (!itemElement || !checkInteract()) return false;

        itemElement.dataset.initialized = 'false';
        initializeItem(itemElement);
        return true;
    };

    // Check if interact.js is available (useful for debugging)
    window.isDynamicGridReady = () => {
        return typeof interact !== 'undefined';
    };

    // Log initialization status when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            console.log('Dynamic Grid System loaded. interact.js available:', typeof interact !== 'undefined');
        });
    } else {
        console.log('Dynamic Grid System loaded. interact.js available:', typeof interact !== 'undefined');
    }

})();
