using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace Demo.Tests;

// Test double only: the application has no fake-model mode.
internal sealed class ScriptedChatClient(params Func<ChatOptions, ChatMessage>[] steps) : IChatClient
{
    private int _step;
    public List<ChatMessage> LastMessages { get; private set; } = [];
    public List<ChatMessage> FirstMessages { get; private set; } = [];
    public string? FirstInstructions { get; private set; }
    public List<string[]> AdvertisedTools { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastMessages = messages.ToList();
        if (_step == 0) FirstMessages = LastMessages;
        options ??= new();
        if (_step == 0) FirstInstructions = options.Instructions;
        AdvertisedTools.Add(options.Tools?.OfType<AIFunctionDeclaration>().Select(t => t.Name).ToArray() ?? []);
        Assert.True(_step < steps.Length, "Unexpected extra model call.");
        return Task.FromResult(new ChatResponse(steps[_step++](options)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }

    public static ChatMessage Call(string name, Dictionary<string, object?> arguments) =>
        new(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString(), name, arguments)]);

    public static ChatMessage Answer(string text) => new(ChatRole.Assistant, text);
}
