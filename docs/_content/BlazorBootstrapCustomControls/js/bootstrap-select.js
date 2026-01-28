/**
 * bootstrap-select.js
 * 
 * Keyboard handling and click-outside detection for BlazorBootstrapSelectSingle and
 * BlazorBootstrapSelectMulti components. See README.md §4 (Keyboard Behavior) for the
 * full specification.
 * 
 * This module provides:
 * - Down/Right/Enter on input: opens the dropdown list (Down/Right highlights first item)
 * - Arrow keys in list: moves keyboard highlight (Down/Right = down, Up/Left = up)
 * - Enter in list: selects/deselects highlighted item
 * - Escape: closes list
 * - Tab: closes list and allows natural focus movement
 * - Input blur: closes list (label, other controls, tab, etc.)
 */

(function () {
  'use strict';

  // Store event handlers per component instance
  var handlers = {};

  /**
   * Get the root element for a component by its unique ID
   * @param {string} id - Component instance ID
   * @returns {Element|null}
   */
  function root(id) {
    return document.querySelector('[data-bs-select-id="' + id + '"]');
  }

  /**
   * Global API exposed to Blazor components
   */
  window.BSSelect = {
    /**
     * Initialize component (reserved for future use).
     * List closes on input blur (handled in Blazor); no click-outside JS.
     * @param {string} componentId - Unique component instance ID
     * @param {DotNetObjectReference} dotNetRef - Reference to Blazor component
     */
    init: function (componentId, dotNetRef) {
      handlers[componentId] = handlers[componentId] || {};
    },

    /**
     * Cleanup: remove all event listeners for this component
     * @param {string} componentId - Unique component instance ID
     */
    teardown: function (componentId) {
      var h = handlers[componentId];
      if (!h) return;
      
      // Remove input keydown listener
      if (h.inputKey) {
        var inp = root(componentId)?.querySelector('.bs-select-input');
        if (inp) inp.removeEventListener('keydown', h.inputKey);
      }
      
      // Remove list keydown listener
      window.BSSelect.unregisterListKeys(componentId);
      
      delete handlers[componentId];
    },

    /**
     * Highlight a specific list item by index (for keyboard navigation)
     * Note: Does NOT move DOM focus - keeps focus on input per §7.4
     * @param {string} componentId - Unique component instance ID
     * @param {number} index - Zero-based index of item to highlight
     */
    focusItem: function (componentId, index) {
      var r = root(componentId);
      if (!r) return;
      var items = r.querySelectorAll('.bs-select-dropdown .bs-select-item');
      var el = items[index];
      if (el) {
        // Remove highlight from all items
        items.forEach(function(item) { item.classList.remove('bs-select-kbd-focus'); });
        // Add highlight class (visual only, no DOM focus)
        el.classList.add('bs-select-kbd-focus');
        el.scrollIntoView({ block: 'nearest' });
      }
    },

    /**
     * Focus the input control (used when closing list)
     * @param {string} componentId - Unique component instance ID
     */
    focusInput: function (componentId) {
      var inp = root(componentId)?.querySelector('.bs-select-input');
      // Don't focus if disabled (tabindex=-1 or has disabled class)
      if (inp && inp.tabIndex !== -1 && !inp.classList.contains('disabled')) {
        inp.focus();
      }
    },

    /**
     * Ensure input has focus (per §7.4: focus ring stays on input)
     * @param {string} componentId - Unique component instance ID
     */
    ensureInputFocus: function (componentId) {
      var inp = root(componentId)?.querySelector('.bs-select-input');
      // Don't focus if disabled (tabindex=-1 or has disabled class)
      if (inp && inp.tabIndex !== -1 && !inp.classList.contains('disabled') && document.activeElement !== inp) {
        inp.focus();
      }
    },

    /**
     * Register keyboard handlers on the input
     * - Down/Right/Enter when closed: opens list (Down/Right highlights first item)
     * - Arrow keys when open: moves highlight (Down/Right = down, Up/Left = up, keeps focus on input per §7.4)
     * @param {string} componentId - Unique component instance ID
     * @param {DotNetObjectReference} dotNetRef - Reference to Blazor component
     */
    registerInputKeys: function (componentId, dotNetRef) {
      var r = root(componentId);
      var inp = r?.querySelector('.bs-select-input');
      if (!inp) return;
      
      var h = function (e) {
        // Don't handle keys if component is disabled
        if (inp.tabIndex === -1 || inp.classList.contains('disabled')) {
          return;
        }
        
        var k = e.key;
        // Check if list is open by checking if dropdown list exists and is visible
        // Fallback to aria-expanded if dropdown doesn't exist yet
        var dropdown = r?.querySelector('.bs-select-dropdown');
        var isOpen = false;
        if (dropdown) {
          // Dropdown exists - check if it's visible
          var style = getComputedStyle(dropdown);
          isOpen = dropdown.offsetParent != null && style.display !== 'none';
        } else {
          // Dropdown doesn't exist - check aria-expanded (Blazor renders as "True"/"False")
          var ariaExpanded = inp.getAttribute('aria-expanded');
          isOpen = ariaExpanded && ariaExpanded.toLowerCase() === 'true';
        }
        
        // When clear button has focus and Enter is pressed, let the button activate (clear) instead of opening the list.
        var isClearButton = e.target.closest && e.target.closest('.bs-select-clear');
        if (isClearButton && k === 'Enter') return;
        
        // When list is open, handle arrow keys, Enter, Escape, and Tab on input (keeps focus on input)
        if (isOpen && ['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'Enter', 'Escape', 'Tab'].indexOf(k) !== -1) {
          // Don't preventDefault on Tab (allow natural focus movement)
          if (k !== 'Tab') {
            e.preventDefault();
            e.stopPropagation();
          }
          dotNetRef.invokeMethodAsync('HandleListKey', k);
          return;
        }
        
        // When list is closed, Down/Right/Enter opens list (§4.1)
        // Down/Right arrow: open and highlight first item; Enter: open without highlighting
        if (!isOpen && (k === 'ArrowDown' || k === 'ArrowRight' || k === 'Enter')) {
          e.preventDefault();
          e.stopPropagation();
          dotNetRef.invokeMethodAsync('OpenFromKey', k === 'ArrowDown' || k === 'ArrowRight');
        }
      };
      inp.addEventListener('keydown', h);
      handlers[componentId] = handlers[componentId] || {};
      handlers[componentId].inputKey = h;
    },

    /**
     * Register keyboard handlers on the dropdown list
     * Handles: ArrowDown/Up/Left/Right (move highlight), Enter (select), Escape (close), Tab (close)
     * @param {string} componentId - Unique component instance ID
     * @param {DotNetObjectReference} dotNetRef - Reference to Blazor component
     */
    registerListKeys: function (componentId, dotNetRef) {
      var r = root(componentId);
      var dropdown = r?.querySelector('.bs-select-dropdown');
      if (!dropdown) return;
      
      var h = function (e) {
        var k = e.key;
        // Only handle navigation and action keys
        if (['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'Enter', 'Escape', 'Tab'].indexOf(k) === -1) return;
        
        // Don't preventDefault on Tab (allow natural focus movement)
        if (k !== 'Tab') {
          e.preventDefault();
          e.stopPropagation();
        }
        
        // Forward to Blazor component for handling
        dotNetRef.invokeMethodAsync('HandleListKey', k);
      };
      dropdown.addEventListener('keydown', h);
      handlers[componentId] = handlers[componentId] || {};
      handlers[componentId].listKey = { dropdown: dropdown, fn: h };
    },

    /**
     * Unregister keyboard handlers from the dropdown list
     * @param {string} componentId - Unique component instance ID
     */
    unregisterListKeys: function (componentId) {
      var h = handlers[componentId];
      if (!h || !h.listKey) return;
      h.listKey.dropdown.removeEventListener('keydown', h.listKey.fn);
      h.listKey = null;
    }
  };
})();
