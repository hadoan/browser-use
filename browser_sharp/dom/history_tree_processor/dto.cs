using System;
using System.Collections.Generic;
namespace browser_sharp.dom.history_tree_processor;


public class HashedDomElement
{
    /// <summary>
    /// Hash of the DOM element to be used as a unique identifier.
    /// </summary>
    public string BranchPathHash { get; set; }
    public string AttributesHash { get; set; }

    public HashedDomElement(string branchPathHash, string attributesHash)
    {
        BranchPathHash = branchPathHash;
        AttributesHash = attributesHash;
    }
}

public class DOMHistoryElement
{
    public string TagName { get; set; }
    public string XPath { get; set; }
    public int? HighlightIndex { get; set; }
    public List<string> EntireParentBranchPath { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
    public bool ShadowRoot { get; set; }

    public DOMHistoryElement(
        string tagName,
        string xPath,
        int? highlightIndex,
        List<string> entireParentBranchPath,
        Dictionary<string, string> attributes,
        bool shadowRoot = false)
    {
        TagName = tagName;
        XPath = xPath;
        HighlightIndex = highlightIndex;
        EntireParentBranchPath = entireParentBranchPath ?? new List<string>();
        Attributes = attributes ?? new Dictionary<string, string>();
        ShadowRoot = shadowRoot;
    }

    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "tag_name", TagName },
            { "xpath", XPath },
            { "highlight_index", HighlightIndex },
            { "entire_parent_branch_path", EntireParentBranchPath },
            { "attributes", Attributes },
            { "shadow_root", ShadowRoot }
        };
    }
}
