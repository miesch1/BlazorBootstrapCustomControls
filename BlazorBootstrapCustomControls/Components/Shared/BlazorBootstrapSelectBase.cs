using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace BlazorBootstrapCustomControls.Components.Shared;

/// <summary>
/// Base class for BlazorBootstrapSelectSingle and BlazorBootstrapSelectMulti components.
/// Contains all common functionality to reduce code duplication.
/// </summary>
/// <typeparam name="TItem">The type of items in the data source</typeparam>
public abstract class BlazorBootstrapSelectBase<TItem> : ComponentBase, IDisposable
{
  [Inject] protected IJSRuntime JS { get; set; } = null!;

  protected string _id = Guid.NewGuid().ToString("N");
  protected DotNetObjectReference<BlazorBootstrapSelectBase<TItem>>? _dotNetRef;
  protected bool _open;
  protected bool _justOpened;
  protected int _highlightedIndex;

  protected List<(string Text, string Value)> _items => (Data ?? Enumerable.Empty<TItem>())
    .Select(x => (TextField?.Invoke(x) ?? x?.ToString() ?? "", ValueField?.Invoke(x) ?? x?.ToString() ?? ""))
    .ToList();

  // Abstract members that must be implemented by derived classes
  protected abstract string DisplayText { get; }
  protected abstract bool IsPlaceholder { get; }
  protected abstract bool ShouldShowClearButton { get; }
  protected abstract bool IsItemSelected(string value);
  protected abstract Task OnClearValue();
  protected abstract Task OnSelectItem(string value);
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
  [Parameter] public Func<TItem, string>? ValueField { get; set; }

  /// <summary>
  /// Gets or sets the width of the select control (e.g., "300px", "50%", "100%").
  /// </summary>
  [Parameter] public string? Width { get; set; }

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

  protected override void OnInitialized()
  {
    // Create DotNetObjectReference - this works because JSInvokable methods are on the base class
    _dotNetRef = DotNetObjectReference.Create(this);
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
      await JS.InvokeVoidAsync("BSSelect.registerListKeys", _id, _dotNetRef);
    }
  }

  public void Dispose()
  {
    _ = JS.InvokeVoidAsync("BSSelect.teardown", _id);
    _dotNetRef?.Dispose();
  }

  [JSInvokable]
  public void CloseFromOutside() => _ = Close(focusInput: false);

  [JSInvokable]
  public void OpenFromKey(bool highlightFirst = false)
  {
    if (_open || Disabled) return;
    _open = true;
    _justOpened = true;
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

  protected async Task Close(bool focusInput)
  {
    if (!_open) return;
    _open = false;
    await JS.InvokeVoidAsync("BSSelect.unregisterListKeys", _id);
    if (focusInput) await JS.InvokeVoidAsync("BSSelect.focusInput", _id);
    StateHasChanged();
  }

  protected void OnInputClick()
  {
    if (Disabled) return;
    _open = !_open;
    if (_open) { _justOpened = true; _highlightedIndex = -1; } /* Don't highlight first item on click */
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

  protected async Task OnItemMouseDown()
  {
    // Prevent focus from moving to the list item on mousedown (per §7.4)
    // Keep focus on input to maintain focus ring
    await JS.InvokeVoidAsync("BSSelect.ensureInputFocus", _id);
  }

  protected async Task SelectItem(string val)
  {
    // Keep focus on input during click (per §7.4) - prevent focus from moving to clicked item
    await JS.InvokeVoidAsync("BSSelect.ensureInputFocus", _id);
    await OnSelectItem(val);
  }
}
