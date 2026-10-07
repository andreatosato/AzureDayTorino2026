using System.ComponentModel;
using System.Net.Http.Json;
using A2A;
using Shared.ServiceDefaults;
using Advance.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Advance.Commerce;

public static class CommerceDemo
{
    public static async Task<string> RunAsync(IChatClient model, HttpClient http, string prompt, CancellationToken ct)
    {
        var skill = new AgentSkillsProvider(Path.Combine(AppContext.BaseDirectory, "skills", "advance"),
            options: new() { DisableLoadSkillApproval = true });
        var agent = new ChatClientAgent(model, new ChatClientAgentOptions
        {
            Name = "CommerceAssistant",
            ChatOptions = new()
            {
                Instructions = Scenario.Context + "Carica la skill commerce-assistance e seguila prima di rispondere.",
                Tools = [AIFunctionFactory.Create(
                    ([Description("La richiesta del cliente con l'identificativo dell'ordine.")] string question,
                        CancellationToken cancellationToken) => DelegateAsync(http, question, cancellationToken),
                    "delegate_logistics", "Delega allo specialista logistico remoto tramite A2A.")]
            },
            AIContextProviders = [skill]
        });
        return (await agent.RunAsync(prompt, cancellationToken: ct)).Text;
    }

    private static async Task<string> DelegateAsync(HttpClient http, string question, CancellationToken ct)
    {
        var card = await http.GetFromJsonAsync<AgentCard>("cards/logistics", ct)
            ?? throw new InvalidOperationException("Agent Card non disponibile.");
        var specialist = card.AsAIAgent(http);
        return (await specialist.RunAsync(question, cancellationToken: ct)).Text;
    }
}
