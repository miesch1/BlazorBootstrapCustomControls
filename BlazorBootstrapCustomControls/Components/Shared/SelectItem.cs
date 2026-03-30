namespace BlazorBootstrapCustomControls.Components.Shared;

public readonly record struct SelectItem<TItem, TValue>(TItem Item, string Text, TValue Value);
