using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace browser_sharp.browser;

public class TabInfo
{
    /// <summary>
    /// Represents information about a browser tab.
    /// </summary>
    public int PageId { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }

    public TabInfo(int pageId, string url, string title)
    {
        PageId = pageId;
        Url = url;
        Title = title;
    }
}

public class BrowserState : DOMState
{
    public string Url { get; set; }
    public string Title { get; set; }
    public List<TabInfo> Tabs { get; set; }
    public string Screenshot { get; set; }
    public int PixelsAbove { get; set; } = 0;
    public int PixelsBelow { get; set; } = 0;
    public List<string> BrowserErrors { get; set; }

    public BrowserState(DOMElementNode elementTree, Dictionary<int, DOMElementNode> selectorMap,
                        string url, string title, List<TabInfo> tabs, string screenshot = null, 
                        int pixelsAbove = 0, int pixelsBelow = 0, List<string> browserErrors = null)
        : base(elementTree, selectorMap)
    {
        Url = url;
        Title = title;
        Tabs = tabs;
        Screenshot = screenshot;
        PixelsAbove = pixelsAbove;
        PixelsBelow = pixelsBelow;
        BrowserErrors = browserErrors ?? new List<string>();
    }
}

public class BrowserStateHistory
{
    public string Url { get; set; }
    public string Title { get; set; }
    public List<TabInfo> Tabs { get; set; }
    public List<DOMHistoryElement> InteractedElement { get; set; }
    public string Screenshot { get; set; }

    public BrowserStateHistory(string url, string title, List<TabInfo> tabs, List<DOMHistoryElement> interactedElement, string screenshot = null)
    {
        Url = url;
        Title = title;
        Tabs = tabs;
        InteractedElement = interactedElement ?? new List<DOMHistoryElement>();
        Screenshot = screenshot;
    }

    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "tabs", Tabs.ConvertAll(tab => JsonSerializer.Serialize(tab)) },
            { "screenshot", Screenshot },
            { "interacted_element", InteractedElement.ConvertAll(el => el != null ? el.ToDictionary() : null) },
            { "url", Url },
            { "title", Title }
        };
    }
}

/// <summary>
/// Base class for all browser-related exceptions.
/// </summary>
public class BrowserError : Exception
{
    public BrowserError(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when a URL is not allowed.
/// </summary>
public class URLNotAllowedError : BrowserError
{
    public URLNotAllowedError(string message) : base(message) { }
}
