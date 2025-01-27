using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.IO;
using System.Reflection;
using System.Text.Json;
namespace browser_sharp.dom;

public class DomService
{
    private readonly IPage _page;
    private readonly Dictionary<string, object> _xpathCache;
    private readonly ILogger<DomService> _logger;

    public DomService(IPage page, ILogger<DomService> logger = null)
    {
        _page = page;
        _xpathCache = new Dictionary<string, object>();
        _logger = logger;
    }

    #region Clickable Elements

    public async Task<DOMState> GetClickableElementsAsync(
        bool highlightElements = true,
        int focusElement = -1,
        int viewportExpansion = 0)
    {
        var elementTree = await BuildDomTreeAsync(highlightElements, focusElement, viewportExpansion);
        var selectorMap = CreateSelectorMap(elementTree);

        return new DOMState(elementTree, selectorMap);
    }

    private async Task<DOMElementNode> BuildDomTreeAsync(
        bool highlightElements,
        int focusElement,
        int viewportExpansion)
    {
        var jsCode = ReadResourceText("browser_use.dom.buildDomTree.js");

        var args = new Dictionary<string, object>
        {
            { "doHighlightElements", highlightElements },
            { "focusHighlightIndex", focusElement },
            { "viewportExpansion", viewportExpansion }
        };

        var evalPage = await _page.EvaluateAsync<string>(jsCode, args);
        var htmlToDict = ParseNode(JsonSerializer.Deserialize<Dictionary<string, object>>(evalPage));

        if (htmlToDict == null || !(htmlToDict is DOMElementNode elementNode))
        {
            throw new InvalidOperationException("Failed to parse HTML to dictionary");
        }

        return elementNode;
    }

    private Dictionary<int, DOMElementNode> CreateSelectorMap(DOMElementNode elementTree)
    {
        var selectorMap = new Dictionary<int, DOMElementNode>();

        void ProcessNode(DOMBaseNode node)
        {
            if (node is DOMElementNode elementNode)
            {
                if (elementNode.HighlightIndex.HasValue)
                {
                    selectorMap[elementNode.HighlightIndex.Value] = elementNode;
                }

                foreach (var child in elementNode.Children)
                {
                    ProcessNode(child);
                }
            }
        }

        ProcessNode(elementTree);
        return selectorMap;
    }

    private DOMBaseNode ParseNode(Dictionary<string, object> nodeData, DOMElementNode parent = null)
    {
        if (nodeData == null || nodeData.Count == 0) return null;

        if (nodeData.TryGetValue("type", out var type) && type.ToString() == "TEXT_NODE")
        {
            return new DOMTextNode(
                isVisible: nodeData.ContainsKey("isVisible") && Convert.ToBoolean(nodeData["isVisible"]),
                parent: parent,
                text: nodeData["text"].ToString()
            );
        }

        var tagName = nodeData["tagName"].ToString();

        var elementNode = new DOMElementNode(
            isVisible: nodeData.ContainsKey("isVisible") && Convert.ToBoolean(nodeData["isVisible"]),
            parent: parent,
            tagName: tagName,
            xPath: nodeData["xpath"].ToString(),
            attributes: nodeData.ContainsKey("attributes") ? JsonSerializer.Deserialize<Dictionary<string, string>>(nodeData["attributes"].ToString()) : new Dictionary<string, string>(),
            children: new List<DOMBaseNode>(),
            isInteractive: nodeData.ContainsKey("isInteractive") && Convert.ToBoolean(nodeData["isInteractive"]),
            isTopElement: nodeData.ContainsKey("isTopElement") && Convert.ToBoolean(nodeData["isTopElement"]),
            highlightIndex: nodeData.ContainsKey("highlightIndex") ? (int?)Convert.ToInt32(nodeData["highlightIndex"]) : null,
            shadowRoot: nodeData.ContainsKey("shadowRoot") && Convert.ToBoolean(nodeData["shadowRoot"])
        );

        var children = new List<DOMBaseNode>();

        if (nodeData.ContainsKey("children") && nodeData["children"] is List<object> childList)
        {
            foreach (var child in childList)
            {
                var childDict = child as Dictionary<string, object>;
                if (childDict != null)
                {
                    var childNode = ParseNode(childDict, elementNode);
                    if (childNode != null)
                    {
                        children.Add(childNode);
                    }
                }
            }
        }

        elementNode.Children = children;
        return elementNode;
    }

    private string ReadResourceText(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Resource {resourceName} not found.");
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    #endregion
}
