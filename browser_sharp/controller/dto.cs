using System;
using System.Text.Json.Serialization;

public record SearchGoogleAction(string Query);

public record GoToUrlAction(string Url);

public record ClickElementAction(int Index, string? Xpath = null);

public record InputTextAction(int Index, string Text, string? Xpath = null);

public record DoneAction(string Text);

public record SwitchTabAction(int PageId);

public record OpenTabAction(string Url);

public record ExtractPageContentAction(bool IncludeLinks);

public record ScrollAction(int? Amount = null);  // The number of pixels to scroll. If null, scrolls down/up one page.

public record SendKeysAction(string Keys);

public record NoParamsAction
{
    /// <summary>
    /// Accepts any incoming data and discards it, returning an empty object.
    /// </summary>
    [JsonConstructor]
    public NoParamsAction() { }
}
