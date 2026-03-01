using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public class DummyClient : IChatClient
{
    public ChatClientMetadata Metadata => new ChatClientMetadata();
    public Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatCompletion(new ChatMessage(ChatRole.Assistant, "Hello")));
    }
    public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return AsyncEnumerable.Empty<StreamingChatCompletionUpdate>();
    }
    public void Dispose() {}
}

public class Test {
    public static void Main() {
        var client = new DummyClient();
        AIAgent agent = new ChatClientAgent(client, "test");
    }
}
