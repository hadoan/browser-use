using System;
using System.Collections.Generic;

public class MessageMetadata
{
    /// <summary>
    /// Metadata for a message including token counts.
    /// </summary>
    public int InputTokens { get; set; } = 0;

    public MessageMetadata(int inputTokens = 0)
    {
        InputTokens = inputTokens;
    }
}

public class ManagedMessage<T> where T : BaseMessage
{
    /// <summary>
    /// A message with its metadata.
    /// </summary>
    public T Message { get; set; }
    public MessageMetadata Metadata { get; set; }

    public ManagedMessage(T message, MessageMetadata metadata)
    {
        Message = message;
        Metadata = metadata;
    }
}

public class MessageHistory<T> where T : BaseMessage
{
    /// <summary>
    /// Container for message history with metadata.
    /// </summary>
    private readonly List<ManagedMessage<T>> _messages = new();
    public IReadOnlyList<ManagedMessage<T>> Messages => _messages.AsReadOnly();
    public int TotalTokens { get; private set; } = 0;

    /// <summary>
    /// Adds a message with metadata to the history.
    /// </summary>
    public void AddMessage(T message, MessageMetadata metadata)
    {
        var managedMessage = new ManagedMessage<T>(message, metadata);
        _messages.Add(managedMessage);
        TotalTokens += metadata.InputTokens;
    }

    /// <summary>
    /// Removes a message from history (default is the last message).
    /// </summary>
    public void RemoveMessage(int index = -1)
    {
        if (_messages.Count > 0)
        {
            if (index == -1) index = _messages.Count - 1; // Default to last message
            if (index < 0 || index >= _messages.Count) return; // Prevent out-of-range errors

            TotalTokens -= _messages[index].Metadata.InputTokens;
            _messages.RemoveAt(index);
        }
    }
}

/// <summary>
/// Base class for messages.
/// </summary>
public abstract class BaseMessage
{
    public string Content { get; set; }

    protected BaseMessage(string content)
    {
        Content = content;
    }
}

/// <summary>
/// AI-generated message.
/// </summary>
public class AIMessage : BaseMessage
{
    public AIMessage(string content) : base(content) { }
}

/// <summary>
/// Human input message.
/// </summary>
public class HumanMessage : BaseMessage
{
    public HumanMessage(string content) : base(content) { }
}

/// <summary>
/// System-related message.
/// </summary>
public class SystemMessage : BaseMessage
{
    public SystemMessage(string content) : base(content) { }
}
