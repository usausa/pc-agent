namespace PcAgent.Tui.Rendering;

using PcAgent.Agent;

using Spectre.Console;

// エージェントの応答ストリーム(AgentEvent)を Spectre でスクロールバックへ逐次描画する。
internal static class ConversationRenderer
{
    public static async Task StreamAsync(IAgentConversation conversation, string message, CancellationToken cancellationToken)
    {
        var agentShown = false;
        await foreach (var agentEvent in conversation.SendAsync(message, cancellationToken))
        {
            switch (agentEvent)
            {
                case ToolCallStarted started:
                    var toolLine = $"[blue]> {Markup.Escape(started.Name)}[/]  [silver]({Markup.Escape(started.Arguments)})[/]";
                    AnsiConsole.MarkupLine(toolLine);
                    agentShown = false;
                    break;
                case TextDelta delta:
                    if (!agentShown)
                    {
                        var label = "[aqua]> agent[/]  ";
                        AnsiConsole.Markup(label);
                        agentShown = true;
                    }

                    AnsiConsole.Markup($"[white]{Markup.Escape(delta.Text)}[/]");
                    break;
                case ResponseCompleted:
                    AnsiConsole.WriteLine();
                    break;
                default:
                    break;
            }
        }
    }
}
