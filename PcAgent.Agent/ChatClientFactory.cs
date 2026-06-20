namespace PcAgent.Agent;

using System.ClientModel;

using Azure.AI.OpenAI;

using OpenAI;
using OpenAI.Chat;

using PcAgent.Agent.Options;

// 設定に応じて IChatClient(OpenAI 互換の ChatClient)を生成する。当面 Foundry、Ollama / Foundry Local も視野。
public static class ChatClientFactory
{
    // 接続情報が不足している場合は null を返す。
    public static ChatClient? TryCreate(LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (String.IsNullOrWhiteSpace(options.Endpoint) ||
            String.IsNullOrWhiteSpace(options.ApiKey) ||
            String.IsNullOrWhiteSpace(options.Model))
        {
            return null;
        }

        var endpoint = new Uri(options.Endpoint);
        var credential = new ApiKeyCredential(options.ApiKey);

        return options.Provider switch
        {
            LlmProvider.Foundry => new AzureOpenAIClient(endpoint, credential).GetChatClient(options.Model),
            LlmProvider.Ollama => new OpenAIClient(credential, new OpenAIClientOptions { Endpoint = endpoint }).GetChatClient(options.Model),
            LlmProvider.FoundryLocal => new OpenAIClient(credential, new OpenAIClientOptions { Endpoint = endpoint }).GetChatClient(options.Model),
            _ => null,
        };
    }
}
