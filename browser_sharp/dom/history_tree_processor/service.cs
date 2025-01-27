using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace browser_sharp.dom.history_tree_processor;

public class HistoryTreeProcessor
{
    /// <summary>
    /// Operations on the DOM elements
    /// 
    /// Be careful - text nodes can change even if elements stay the same.
    /// </summary>
    public static DOMHistoryElement ConvertDomElementToHistoryElement(DOMElementNode domElement)
    {
        var parentBranchPath = GetParentBranchPath(domElement);
        return new DOMHistoryElement(
            domElement.TagName,
            domElement.XPath,
            domElement.HighlightIndex,
            parentBranchPath,
            domElement.Attributes,
            domElement.ShadowRoot
        );
    }

    public static DOMElementNode FindHistoryElementInTree(DOMHistoryElement domHistoryElement, DOMElementNode tree)
    {
        var hashedDomHistoryElement = HashDomHistoryElement(domHistoryElement);

        DOMElementNode ProcessNode(DOMElementNode node)
        {
            if (node.HighlightIndex != null)
            {
                var hashedNode = HashDomElement(node);
                if (hashedNode.Equals(hashedDomHistoryElement))
                {
                    return node;
                }
            }
            foreach (var child in node.Children)
            {
                if (child is DOMElementNode domChild)
                {
                    var result = ProcessNode(domChild);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        return ProcessNode(tree);
    }

    public static bool CompareHistoryElementAndDomElement(DOMHistoryElement domHistoryElement, DOMElementNode domElement)
    {
        var hashedDomHistoryElement = HashDomHistoryElement(domHistoryElement);
        var hashedDomElement = HashDomElement(domElement);

        return hashedDomHistoryElement.Equals(hashedDomElement);
    }

    private static HashedDomElement HashDomHistoryElement(DOMHistoryElement domHistoryElement)
    {
        var branchPathHash = ParentBranchPathHash(domHistoryElement.EntireParentBranchPath);
        var attributesHash = AttributesHash(domHistoryElement.Attributes);

        return new HashedDomElement(branchPathHash, attributesHash);
    }

    private static HashedDomElement HashDomElement(DOMElementNode domElement)
    {
        var parentBranchPath = GetParentBranchPath(domElement);
        var branchPathHash = ParentBranchPathHash(parentBranchPath);
        var attributesHash = AttributesHash(domElement.Attributes);
        // string textHash = TextHash(domElement);

        return new HashedDomElement(branchPathHash, attributesHash);
    }

    private static List<string> GetParentBranchPath(DOMElementNode domElement)
    {
        var parents = new List<DOMElementNode>();
        var currentElement = domElement;

        while (currentElement.Parent != null)
        {
            parents.Add(currentElement);
            currentElement = currentElement.Parent;
        }

        parents.Reverse();
        return parents.ConvertAll(parent => parent.TagName);
    }

    private static string ParentBranchPathHash(List<string> parentBranchPath)
    {
        var parentBranchPathString = string.Join("/", parentBranchPath);
        return ComputeSha256Hash(parentBranchPathString);
    }

    private static string AttributesHash(Dictionary<string, string> attributes)
    {
        var attributesString = string.Join("", attributes.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return ComputeSha256Hash(attributesString);
    }

    private static string TextHash(DOMElementNode domElement)
    {
        var textString = domElement.GetAllTextTillNextClickableElement();
        return ComputeSha256Hash(textString);
    }

    private static string ComputeSha256Hash(string input)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}
