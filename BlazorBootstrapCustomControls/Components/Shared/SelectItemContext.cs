namespace BlazorBootstrapCustomControls.Components.Shared;

public readonly record struct SelectItemContext<TItem, TValue>(
  TItem Item,
  TValue Value,
  string Text,
  bool IsSelected,
  bool IsHighlighted,
  SelectSemantic Semantic,
  string? SemanticText
);
