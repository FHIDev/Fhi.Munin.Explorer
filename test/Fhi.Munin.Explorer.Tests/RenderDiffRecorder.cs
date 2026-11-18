using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable BL0006 // RenderTree types are what this recorder exists to read.

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A renderer that keeps Blazor's own diffs, which bUnit does not: bUnit parses the whole markup again on every
/// render, so an element the diff removed and inserted again looks the same as one it kept.
/// </summary>
internal sealed class RenderDiffRecorder(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
{
    private readonly List<string> _inserted = [];

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    /// <summary>
    /// For each frame the diffs inserted, the names of the attribute frames in its subtree joined by spaces: an element's
    /// attributes and its descendants', a child component's parameter names, and empty for text.
    /// </summary>
    public IReadOnlyList<string> Inserted
    {
        get
        {
            lock (_inserted)
            {
                return [.. _inserted];
            }
        }
    }

    public async Task<int> RenderAsync<TComponent>(IDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var component = InstantiateComponent(typeof(TComponent));
        var id = AssignRootComponentId(component);
        await Dispatcher.InvokeAsync(() => RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters)));
        return id;
    }

    /// <summary>
    /// The handler id of the first <paramref name="eventName"/> on an element whose class holds <paramref name="cssClass"/>,
    /// in the component or the components it renders.
    /// </summary>
    public ulong HandlerOf(int componentId, string cssClass, string eventName) =>
        FindHandler(componentId, cssClass, eventName) ?? throw new InvalidOperationException($"no {eventName} on .{cssClass}");

    private ulong? FindHandler(int componentId, string cssClass, string eventName)
    {
        var frames = GetCurrentRenderTreeFrames(componentId);
        var children = new List<int>();

        for (var i = 0; i < frames.Count; i++)
        {
            ref var frame = ref frames.Array[i];

            if (frame.FrameType == RenderTreeFrameType.Component)
            {
                children.Add(frame.ComponentId);
            }

            if (frame.FrameType != RenderTreeFrameType.Element)
            {
                continue;
            }

            var matches = false;
            ulong? handler = null;

            for (var a = i + 1; a < frames.Count && frames.Array[a].FrameType == RenderTreeFrameType.Attribute; a++)
            {
                ref var attribute = ref frames.Array[a];
                matches |= attribute.AttributeName == "class" && attribute.AttributeValue is string css && css.Split(' ').Contains(cssClass);
                handler = attribute.AttributeName == eventName ? attribute.AttributeEventHandlerId : handler;
            }

            if (matches && handler is { } found)
            {
                return found;
            }
        }

        return children.Select(c => FindHandler(c, cssClass, eventName)).FirstOrDefault(h => h is not null);
    }

    public Task ClickAsync(ulong handlerId, EventArgs args) =>
        Dispatcher.InvokeAsync(() => DispatchEventAsync(handlerId, null, args));

    protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        var references = renderBatch.ReferenceFrames;

        for (var c = 0; c < renderBatch.UpdatedComponents.Count; c++)
        {
            var diff = renderBatch.UpdatedComponents.Array[c];

            for (var e = 0; e < diff.Edits.Count; e++)
            {
                var edit = diff.Edits.Array[diff.Edits.Offset + e];

                if (edit.Type != RenderTreeEditType.PrependFrame)
                {
                    continue;
                }

                var start = edit.ReferenceFrameIndex;
                ref var top = ref references.Array[start];
                var length = top.FrameType switch
                {
                    RenderTreeFrameType.Element => top.ElementSubtreeLength,
                    RenderTreeFrameType.Region => top.RegionSubtreeLength,
                    RenderTreeFrameType.Component => top.ComponentSubtreeLength,
                    _ => 1,
                };
                var names = new List<string>();

                for (var i = start; i < start + length; i++)
                {
                    if (references.Array[i].FrameType == RenderTreeFrameType.Attribute)
                    {
                        names.Add(references.Array[i].AttributeName);
                    }
                }

                lock (_inserted)
                {
                    _inserted.Add(string.Join(' ', names));
                }
            }
        }

        return Task.CompletedTask;
    }
}
