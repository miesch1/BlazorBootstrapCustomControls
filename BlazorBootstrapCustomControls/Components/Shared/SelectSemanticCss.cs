namespace BlazorBootstrapCustomControls.Components.Shared;

/// <summary>
/// Maps <see cref="SelectSemantic"/> to the CSS modifier classes used by library styles
/// (<c>status-chip-*</c> in <c>bootstrap-select.css</c>). Use the same helpers in grids, labels, and other UI
/// so semantics stay visually consistent with the select’s default chips.
/// </summary>
public static class SelectSemanticCss
{
  public const string ChipBaseClass = "status-chip";

  /// <summary>Modifier only (e.g. <c>status-chip-warning</c>). Pair with <see cref="ChipBaseClass"/> or use <see cref="GetChipClasses"/>.</summary>
  public static string GetChipModifierClass(SelectSemantic semantic) =>
    semantic switch
    {
      SelectSemantic.Pending => "status-chip-pending",
      SelectSemantic.Info => "status-chip-info",
      SelectSemantic.Success => "status-chip-success",
      SelectSemantic.Warning => "status-chip-warning",
      SelectSemantic.Danger => "status-chip-danger",
      _ => "status-chip-none"
    };

  /// <summary>Full class string: <c>status-chip status-chip-*</c>.</summary>
  public static string GetChipClasses(SelectSemantic semantic) =>
    $"{ChipBaseClass} {GetChipModifierClass(semantic)}";
}
