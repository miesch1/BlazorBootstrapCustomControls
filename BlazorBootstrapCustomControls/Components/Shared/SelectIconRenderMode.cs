namespace BlazorBootstrapCustomControls.Components.Shared;

/// <summary>
/// How <see cref="BlazorBootstrapSelectBase{TItem, TValue}.IconContent"/> values are rendered when using the convenience icon API (no <c>ItemTemplate</c>).
/// </summary>
public enum SelectIconRenderMode
{
  /// <summary>Icon string is a CSS class list (e.g. Bootstrap Icons: <c>bi bi-trophy</c>).</summary>
  CssClass = 0,

  /// <summary>Icon string is plain text (e.g. emoji or a single glyph).</summary>
  Text = 1
}
