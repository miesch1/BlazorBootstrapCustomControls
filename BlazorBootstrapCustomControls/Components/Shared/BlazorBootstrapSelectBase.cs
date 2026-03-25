using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;

namespace BlazorBootstrapCustomControls.Components.Shared;

/// <summary>
/// Base class for BlazorBootstrapSelectSingle and BlazorBootstrapSelectMulti components.
/// Contains all common functionality to reduce code duplication.
/// </summary>
/// <typeparam name="TItem">The type of items in the data source</typeparam>
/// <typeparam name="TValue">The type of the selected value</typeparam>
public abstract class BlazorBootstrapSelectBase<TItem, TValue> : ComponentBase, IAsyncDisposable
{
  [Inject] protected IJSRuntime JS { get; set; } = null!;

  protected string _id = Guid.NewGuid().ToString("N");
  protected DotNetObjectReference<BlazorBootstrapSelectBase<TItem, TValue>>? _dotNetRef;
  private bool _disposed;
  protected bool _open;
  protected bool _justOpened;
  protected int _highlightedIndex;
  private string _typeAheadBuffer = string.Empty;
  private DateTime _lastTypeAheadInputUtc = DateTime.MinValue;
  private const int TypeAheadResetMilliseconds = 700;

  /// <summary>Snapshot of <see cref="Data"/> built in <see cref="OnParametersSet"/> (avoids reallocating on every access).</summary>
  protected IReadOnlyList<SelectItem<TValue>> _items = Array.Empty<SelectItem<TValue>>();

  private TValue GetValue(TItem item)
  {
    if (ValueField != null)
    {
      return ValueField(item);
    }

    if (item is TValue v)
    {
      return v;
    }

    if (typeof(TValue) == typeof(string))
    {
      return (TValue)(object)(item?.ToString() ?? "");
    }

    throw new InvalidOperationException("ValueField must be provided when TItem is not TValue.");
  }

  // Abstract members that must be implemented by derived classes
  protected abstract string DisplayText { get; }
  protected abstract bool IsPlaceholder { get; }
  protected abstract bool ShouldShowClearButton { get; }
  protected abstract bool IsItemSelected(TValue value);
  protected abstract Task OnClearValue();
  protected abstract Task OnSelectItem(TValue value);
  protected abstract Task OnHandleEnterKey(int highlightedIndex);

  /// <summary>
  /// Gets or sets the label text displayed above the select control.
  /// </summary>
  [Parameter] public string? Label { get; set; }

  /// <summary>
  /// Gets or sets the placeholder text displayed when no value is selected.
  /// </summary>
  [Parameter] public string? PlaceholderText { get; set; }

  /// <summary>
  /// Gets or sets the data source collection of items to display in the dropdown.
  /// </summary>
  [Parameter] public IEnumerable<TItem>? Data { get; set; }

  /// <summary>
  /// Gets or sets the function used to extract the display text from each item.
  /// If not provided, the item's ToString() method will be used.
  /// </summary>
  [Parameter] public Func<TItem, string>? TextField { get; set; }

  /// <summary>
  /// Gets or sets the function used to extract the value from each item.
  /// If not provided, the item's ToString() method will be used.
  /// </summary>
  [Parameter] public Func<TItem, TValue>? ValueField { get; set; }

  /// <summary>
  /// Gets or sets the width of the select control (e.g., "300px", "50%", "100%").
  /// </summary>
  [Parameter] public string? Width { get; set; }

  /// <summary>
  /// Additional CSS classes applied to the interactive input surface (the form-control-like element).
  /// This is the preferred way to apply Bootstrap classes such as "is-invalid".
  /// </summary>
  [Parameter] public string? Class { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the control is disabled.
  /// When disabled, the control cannot be interacted with and appears grayed out.
  /// </summary>
  [Parameter] public bool Disabled { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the clear button should be shown.
  /// The clear button will only appear when there is a selected value and this property is true.
  /// </summary>
  [Parameter] public bool ShowClearButton { get; set; } = true;

  /// <summary>
  /// Gets or sets a value indicating whether the input grows vertically when displayed
  /// text wraps (e.g. when Width is set and content exceeds it). When false (default),
  /// overflow is truncated with an ellipsis ("...").
  /// </summary>
  [Parameter] public bool AutoExpandVertically { get; set; }

  /// <summary>
  /// When true, the input is rendered in an invalid state (e.g., red border and focus ring),
  /// mirroring Bootstrap's "is-invalid" contract. This is additive with any classes passed via Class.
  /// </summary>
  [Parameter] public bool IsInvalid { get; set; }

  /// <summary>
  /// When false (default), the dropdown list width is auto-sized to the longest item.
  /// When true, the dropdown list width matches the select input width.
  /// </summary>
  [Parameter] public bool DropdownMatchInputWidth { get; set; }

  /// <summary>
  /// Additional attributes applied to the interactive input surface (the form-control-like element).
  /// Use this to attach data-* attributes or ARIA attributes directly to the control.
  /// </summary>
  [Parameter] public IDictionary<string, object>? InputAttributes { get; set; }

  /// <summary>
  /// Gets or sets additional attributes to apply to the component's outer element.
  /// </summary>
  [Parameter(CaptureUnmatchedValues = true)]
  public IDictionary<string, object>? AdditionalAttributes { get; set; }

  protected override void OnInitialized()
  {
    // Create DotNetObjectReference - this works because JSInvokable methods are on the base class
    _dotNetRef = DotNetObjectReference.Create(this);
  }

  protected override void OnParametersSet()
  {
    _items = (Data ?? Enumerable.Empty<TItem>())
      .Select(x => new SelectItem<TValue>(TextField?.Invoke(x) ?? x?.ToString() ?? "", GetValue(x)))
      .ToList();
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      await JS.InvokeVoidAsync("BSSelect.init", _id, _dotNetRef);
      await JS.InvokeVoidAsync("BSSelect.registerInputKeys", _id, _dotNetRef);
    }
    if (_open && _justOpened)
    {
      _justOpened = false;
      // Keep focus on input (per §7.4) - don't move focus to list items
      await JS.InvokeVoidAsync("BSSelect.ensureInputFocus", _id);
      // Only highlight if index is valid (>= 0), not on initial open
      if (_highlightedIndex >= 0)
      {
        await JS.InvokeVoidAsync("BSSelect.focusItem", _id, _highlightedIndex);
      }
    }
  }

  /// <summary>
  /// Blazor awaits this when the component is removed, so JS teardown and <see cref="DotNetObjectReference{TValue}"/>
  /// disposal complete before the instance is released (no fire-and-forget from sync <c>IDisposable</c>).
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    if (_disposed) return;
    _disposed = true;

    try
    {
      await JS.InvokeVoidAsync("BSSelect.teardown", _id);
    }
    catch (JSDisconnectedException)
    {
    }
    finally
    {
      _dotNetRef?.Dispose();
      _dotNetRef = null;
    }
  }

  [JSInvokable]
  public void OpenFromKey(bool highlightFirst = false)
  {
    if (_open || Disabled) return;
    _open = true;
    _justOpened = true;
    _typeAheadBuffer = string.Empty;
    _lastTypeAheadInputUtc = DateTime.MinValue;
    // When Down arrow opens the list, highlight first item; otherwise don't highlight (Enter key or mouse click)
    _highlightedIndex = highlightFirst ? 0 : -1;
    StateHasChanged();
  }

  [JSInvokable]
  public async Task HandleListKey(string key)
  {
    var n = _items.Count;
    if (n == 0) return;

    if (key == "Escape") { await Close(focusInput: true); return; }
    if (key == "Tab") { await Close(focusInput: false); return; }

    if (key == "ArrowDown" || key == "ArrowRight")
    {
      // If no item is highlighted, start at index 0; otherwise move down
      if (_highlightedIndex < 0) _highlightedIndex = 0;
      else if (_highlightedIndex < n - 1) _highlightedIndex++;
      await JS.InvokeVoidAsync("BSSelect.focusItem", _id, _highlightedIndex);
      StateHasChanged();
      return;
    }
    if (key == "ArrowUp" || key == "ArrowLeft")
    {
      if (_highlightedIndex <= 0) { await Close(focusInput: true); return; }
      _highlightedIndex--;
      await JS.InvokeVoidAsync("BSSelect.focusItem", _id, _highlightedIndex);
      StateHasChanged();
      return;
    }
    if (key == "Enter")
    {
      // Only select if an item is highlighted
      if (_highlightedIndex >= 0 && _highlightedIndex < n)
      {
        await OnHandleEnterKey(_highlightedIndex);
      }
      return;
    }
  }

  /// <summary>
  /// Handles type-ahead while the list is open. Typing printable characters highlights
  /// the first matching item by text (prefix match, case-insensitive).
  /// </summary>
  [JSInvokable]
  public Task HandleTypeAhead(string key) =>
    !_open || string.IsNullOrEmpty(key) ? Task.CompletedTask : ApplyTypeAheadKeyAsync(key);

  /// <summary>
  /// When the list is closed, typing a printable character opens the list and highlights
  /// the first item whose text starts with that prefix (same buffer/timeout rules as when open).
  /// </summary>
  [JSInvokable]
  public async Task OpenAndTypeAhead(string key)
  {
    if (Disabled || string.IsNullOrEmpty(key)) return;
    if (_items.Count == 0) return;

    if (!_open)
    {
      _open = true;
      _justOpened = true;
      _typeAheadBuffer = string.Empty;
      _lastTypeAheadInputUtc = DateTime.MinValue;
      _highlightedIndex = -1;
    }

    await ApplyTypeAheadKeyAsync(key);
  }

  private async Task ApplyTypeAheadKeyAsync(string key)
  {
    if (string.IsNullOrEmpty(key)) return;
    if (_items.Count == 0) return;

    var now = DateTime.UtcNow;
    if ((now - _lastTypeAheadInputUtc).TotalMilliseconds > TypeAheadResetMilliseconds)
    {
      _typeAheadBuffer = string.Empty;
    }
    _lastTypeAheadInputUtc = now;

    _typeAheadBuffer += key;

    var index = FindPrefixMatchIndex(_typeAheadBuffer);

    // If a longer buffer has no match, retry with only the latest character.
    if (index < 0 && _typeAheadBuffer.Length > 1)
    {
      _typeAheadBuffer = key;
      index = FindPrefixMatchIndex(_typeAheadBuffer);
    }

    if (index >= 0)
    {
      _highlightedIndex = index;
      await JS.InvokeVoidAsync("BSSelect.focusItem", _id, _highlightedIndex);
    }

    StateHasChanged();
  }

  private int FindPrefixMatchIndex(string prefix)
  {
    if (string.IsNullOrWhiteSpace(prefix)) return -1;

    for (var i = 0; i < _items.Count; i++)
    {
      if (_items[i].Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      {
        return i;
      }
    }

    return -1;
  }

  protected async Task Close(bool focusInput)
  {
    if (!_open) return;
    _open = false;
    _typeAheadBuffer = string.Empty;
    _lastTypeAheadInputUtc = DateTime.MinValue;
    if (focusInput) await JS.InvokeVoidAsync("BSSelect.focusInput", _id);
    StateHasChanged();
  }

  protected void OnInputClick()
  {
    if (Disabled) return;
    if (_open)
    {
      _ = Close(focusInput: false);
      return;
    }

    _open = true;
    _justOpened = true;
    _highlightedIndex = -1; /* Don't highlight first item on click */
    _typeAheadBuffer = string.Empty;
    _lastTypeAheadInputUtc = DateTime.MinValue;
    StateHasChanged();
  }

  /// <summary>
  /// Close the list when the input loses focus (label, other controls, tab, etc.).
  /// Keeps focus-based close logic in one place; no click-outside special cases.
  /// </summary>
  protected void OnInputBlur() => _ = Close(focusInput: false);

  protected void OnItemHover(int index)
  {
    if (_highlightedIndex != index)
    {
      _highlightedIndex = index;
      StateHasChanged();
    }
  }

  protected async Task OnClear()
  {
    if (Disabled) return; // Prevent clearing when disabled
    await OnClearValue();
    await JS.InvokeVoidAsync("BSSelect.focusInput", _id);
    StateHasChanged();
  }

  /// <summary>
  /// Invoked from JS when user presses Delete with input focused and a value selected. Same behavior as clear button click.
  /// </summary>
  [JSInvokable]
  public async Task ClearFromKey()
  {
    await OnClear();
  }

  protected async Task OnItemMouseDown()
  {
    // Prevent focus from moving to the list item on mousedown (per §7.4)
    // Keep focus on input to maintain focus ring
    await JS.InvokeVoidAsync("BSSelect.ensureInputFocus", _id);
  }

  protected async Task SelectItem(TValue val)
  {
    // Keep focus on input during click (per §7.4) - prevent focus from moving to clicked item
    await JS.InvokeVoidAsync("BSSelect.ensureInputFocus", _id);
    await OnSelectItem(val);
  }
}
