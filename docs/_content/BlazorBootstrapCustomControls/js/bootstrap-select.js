/**
 * bootstrap-select.js
 * 
 * Keyboard handling for BlazorBootstrapSelectSingle and BlazorBootstrapSelectMulti. See README.md §4.
 * 
 * This module provides:
 * - Down/Right/Enter on input: opens the dropdown list (Down/Right highlights first item)
 * - Arrow keys when open: moves highlight (forwarded to Blazor HandleListKey; focus stays on input per §7.4)
 * - Enter in list: selects/deselects highlighted item
 * - Escape: closes list
 * - Tab: closes list and allows natural focus movement
 * - Delete (when list closed): clears selection when clear button is shown
 * - Printable keys: type-ahead highlight by item prefix (opens list when closed); skipped during IME composition
 * - Input blur: closes list (label, other controls, tab, etc.)
 * - Dropdown panel mousedown: default prevented in markup so scrollbar drag does not blur the input (issue #3)
 */

(function () {
  'use strict';

  var handlers = {};

  function root(id) {
    return document.querySelector('[data-bs-select-id="' + id + '"]');
  }

  window.BSSelect = {
    init: function (componentId, dotNetRef) {
      handlers[componentId] = handlers[componentId] || {};
    },

    teardown: function (componentId) {
      var h = handlers[componentId];
      if (!h) return;

      if (h.inputKey) {
        var inp = root(componentId)?.querySelector('.bs-select-input');
        if (inp) {
          inp.removeEventListener('keydown', h.inputKey);
          if (h.compStart) inp.removeEventListener('compositionstart', h.compStart);
          if (h.compEnd) inp.removeEventListener('compositionend', h.compEnd);
        }
      }

      delete handlers[componentId];
    },

    focusItem: function (componentId, index) {
      var r = root(componentId);
      if (!r) return;
      var items = r.querySelectorAll('.bs-select-dropdown .bs-select-item');
      var el = items[index];
      if (el) {
        items.forEach(function(item) { item.classList.remove('bs-select-kbd-focus'); });
        el.classList.add('bs-select-kbd-focus');
        el.scrollIntoView({ block: 'nearest' });
      }
    },

    focusInput: function (componentId) {
      var inp = root(componentId)?.querySelector('.bs-select-input');
      if (inp && inp.tabIndex !== -1 && !inp.classList.contains('disabled')) {
        inp.focus();
      }
    },

    ensureInputFocus: function (componentId) {
      var inp = root(componentId)?.querySelector('.bs-select-input');
      if (inp && inp.tabIndex !== -1 && !inp.classList.contains('disabled') && document.activeElement !== inp) {
        inp.focus();
      }
    },

    registerInputKeys: function (componentId, dotNetRef) {
      var r = root(componentId);
      var inp = r?.querySelector('.bs-select-input');
      if (!inp) return false;

      // Idempotent: if this component was already wired (e.g. re-run on same DOM node),
      // remove the previous listeners before attaching. Otherwise keydown runs twice per
      // physical key — extra arrow steps, broken type-ahead buffer, double Enter toggles.
      var prev = handlers[componentId];
      if (prev && prev.inputKey) {
        inp.removeEventListener('keydown', prev.inputKey);
        if (prev.compStart) inp.removeEventListener('compositionstart', prev.compStart);
        if (prev.compEnd) inp.removeEventListener('compositionend', prev.compEnd);
      }

      var composing = false;
      function onCompStart() { composing = true; }
      function onCompEnd() { composing = false; }

      inp.addEventListener('compositionstart', onCompStart);
      inp.addEventListener('compositionend', onCompEnd);

      var h = function (e) {
        if (inp.tabIndex === -1 || inp.classList.contains('disabled')) {
          return;
        }

        if (e.isComposing || composing) {
          return;
        }

        var k = e.key;
        var dropdown = r?.querySelector('.bs-select-dropdown');
        var isOpen = false;
        if (dropdown) {
          var style = getComputedStyle(dropdown);
          isOpen = dropdown.offsetParent != null && style.display !== 'none';
        } else {
          var ariaExpanded = inp.getAttribute('aria-expanded');
          isOpen = ariaExpanded && ariaExpanded.toLowerCase() === 'true';
        }

        // Key-repeat on Enter would invoke HandleListKey twice (multi-select: select then undo).
        if (isOpen && k === 'Enter' && e.repeat) {
          e.preventDefault();
          e.stopPropagation();
          return;
        }

        if (!isOpen && k === 'Delete' && r.querySelector('.bs-select-clear')) {
          e.preventDefault();
          e.stopPropagation();
          dotNetRef.invokeMethodAsync('ClearFromKey');
          return;
        }

        if (!isOpen && !e.ctrlKey && !e.altKey && !e.metaKey && k.length === 1) {
          e.preventDefault();
          e.stopPropagation();
          dotNetRef.invokeMethodAsync('OpenAndTypeAhead', k);
          return;
        }

        if (isOpen && ['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'Enter', 'Escape', 'Tab'].indexOf(k) !== -1) {
          if (k !== 'Tab') {
            e.preventDefault();
            e.stopPropagation();
          }
          dotNetRef.invokeMethodAsync('HandleListKey', k);
          return;
        }

        if (isOpen && !e.ctrlKey && !e.altKey && !e.metaKey && (k.length === 1)) {
          e.preventDefault();
          e.stopPropagation();
          dotNetRef.invokeMethodAsync('HandleTypeAhead', k);
          return;
        }

        if (!isOpen && (k === 'ArrowDown' || k === 'ArrowRight' || k === 'Enter')) {
          e.preventDefault();
          e.stopPropagation();
          dotNetRef.invokeMethodAsync('OpenFromKey', k === 'ArrowDown' || k === 'ArrowRight');
        }
      };
      inp.addEventListener('keydown', h);
      handlers[componentId] = handlers[componentId] || {};
      handlers[componentId].inputKey = h;
      handlers[componentId].compStart = onCompStart;
      handlers[componentId].compEnd = onCompEnd;
      return true;
    }
  };
})();
