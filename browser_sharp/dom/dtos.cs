using System;
using System.Collections.Generic;
using System.Linq;
namespace browser_sharp.dom;
public class DOMBaseNode
{
    public bool IsVisible { get; set; }
    public DOMElementNode Parent { get; set; }

    public DOMBaseNode(bool isVisible, DOMElementNode parent = null)
    {
        IsVisible = isVisible;
        Parent = parent;
    }
}

public class DOMTextNode : DOMBaseNode
{
    public string Text { get; set; }
    public string Type { get; } = "TEXT_NODE";

    public DOMTextNode(bool isVisible, DOMElementNode parent, string text)
        : base(isVisible, parent)
    {
        Text = text;
    }

    public bool HasParentWithHighlightIndex()
    {
        var current = Parent;
        while (current != null)
        {
            if (current.HighlightIndex.HasValue)
            {
                return true;
            }
            current = current.Parent;
        }
        return false;
    }
}

public class DOMElementNode : DOMBaseNode
{
    public string TagName { get; set; }
    public string XPath { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
    public List<DOMBaseNode> Children { get; set; }
    public bool IsInteractive { get; set; }
    public bool IsTopElement { get; set; }
    public bool ShadowRoot { get; set; }
    public int? HighlightIndex { get; set; }

    public DOMElementNode(
        bool isVisible,
        DOMElementNode parent,
        string tagName,
        string xPath,
        Dictionary<string, string> attributes,
        List<DOMBaseNode> children,
        bool isInteractive = false,
        bool isTopElement = false,
        bool shadowRoot = false,
        int? highlightIndex = null
    ) : base(isVisible, parent)
    {
        TagName = tagName;
        XPath = xPath;
        Attributes = attributes ?? new Dictionary<string, string>();
        Children = children ?? new List<DOMBaseNode>();
        IsInteractive = isInteractive;
        IsTopElement = isTopElement;
        ShadowRoot = shadowRoot;
        HighlightIndex = highlightIndex;
    }

    public override string ToString()
    {
        string tagStr = $"<{TagName}";

        foreach (var kvp in Attributes)
        {
            tagStr += $" {kvp.Key}=\"{kvp.Value}\"";
        }
        tagStr += ">";

        var extras = new List<string>();
        if (IsInteractive) extras.Add("interactive");
        if (IsTopElement) extras.Add("top");
        if (ShadowRoot) extras.Add("shadow-root");
        if (HighlightIndex.HasValue) extras.Add($"highlight:{HighlightIndex}");

        if (extras.Any())
        {
            tagStr += $" [{string.Join(", ", extras)}]";
        }

        return tagStr;
    }

    public string GetAllTextTillNextClickableElement(int maxDepth = -1)
    {
        var textParts = new List<string>();

        void CollectText(DOMBaseNode node, int currentDepth)
        {
            if (maxDepth != -1 && currentDepth > maxDepth) return;

            if (node is DOMElementNode elementNode && elementNode != this && elementNode.HighlightIndex.HasValue)
            {
                return;
            }

            if (node is DOMTextNode textNode)
            {
                textParts.Add(textNode.Text);
            }
            else if (node is DOMElementNode elementNode)
            {
                foreach (var child in elementNode.Children)
                {
                    CollectText(child, currentDepth + 1);
                }
            }
        }

        CollectText(this, 0);
        return string.Join("\n", textParts).Trim();
    }

    public string ClickableElementsToString(List<string> includeAttributes = null)
    {
        var formattedText = new List<string>();

        void ProcessNode(DOMBaseNode node, int depth)
        {
            if (node is DOMElementNode elementNode)
            {
                if (elementNode.HighlightIndex.HasValue)
                {
                    var attributesStr = "";
                    if (includeAttributes != null)
                    {
                        attributesStr = " " + string.Join(
                            " ",
                            elementNode.Attributes
                                .Where(kvp => includeAttributes.Contains(kvp.Key))
                                .Select(kvp => $"{kvp.Key}=\"{kvp.Value}\"")
                        );
                    }

                    formattedText.Add(
                        $"{elementNode.HighlightIndex}[:]<{elementNode.TagName}{attributesStr}>{elementNode.GetAllTextTillNextClickableElement()}</{elementNode.TagName}>"
                    );
                }

                foreach (var child in elementNode.Children)
                {
                    ProcessNode(child, depth + 1);
                }
            }
            else if (node is DOMTextNode textNode && !textNode.HasParentWithHighlightIndex())
            {
                formattedText.Add($"_[:]{textNode.Text}");
            }
        }

        ProcessNode(this, 0);
        return string.Join("\n", formattedText);
    }

    public DOMElementNode GetFileUploadElement(bool checkSiblings = true)
    {
        if (TagName == "input" && Attributes.TryGetValue("type", out var type) && type == "file")
        {
            return this;
        }

        foreach (var child in Children)
        {
            if (child is DOMElementNode childElement)
            {
                var result = childElement.GetFileUploadElement(false);
                if (result != null) return result;
            }
        }

        if (checkSiblings && Parent != null)
        {
            foreach (var sibling in Parent.Children)
            {
                if (sibling != this && sibling is DOMElementNode siblingElement)
                {
                    var result = siblingElement.GetFileUploadElement(false);
                    if (result != null) return result;
                }
            }
        }

        return null;
    }
}

public class ElementTreeSerializer
{
    public static string SerializeClickableElements(DOMElementNode elementTree)
    {
        return elementTree.ClickableElementsToString();
    }

    public static Dictionary<string, object> DomElementNodeToJson(DOMElementNode elementTree)
    {
        Dictionary<string, object> NodeToDict(DOMBaseNode node)
        {
            if (node is DOMTextNode textNode)
            {
                return new Dictionary<string, object>
                {
                    { "type", "text" },
                    { "text", textNode.Text }
                };
            }
            else if (node is DOMElementNode elementNode)
            {
                return new Dictionary<string, object>
                {
                    { "type", "element" },
                    { "tag_name", elementNode.TagName },
                    { "attributes", elementNode.Attributes },
                    { "highlight_index", elementNode.HighlightIndex },
                    { "children", elementNode.Children.ConvertAll(child => NodeToDict(child)) }
                };
            }
            return new Dictionary<string, object>();
        }

        return NodeToDict(elementTree);
    }
}

public class DOMState
{
    public DOMElementNode ElementTree { get; set; }
    public Dictionary<int, DOMElementNode> SelectorMap { get; set; }

    public DOMState(DOMElementNode elementTree, Dictionary<int, DOMElementNode> selectorMap)
    {
        ElementTree = elementTree;
        SelectorMap = selectorMap;
    }
}
