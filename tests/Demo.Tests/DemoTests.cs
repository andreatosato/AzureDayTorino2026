using System.Net;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Runtime.CompilerServices;
using A2A;
using Shared.ServiceDefaults;
using Basic.Data;
using Basic.Web;
using Advance.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Demo.Tests;

public sealed class DemoTests
{
    [Theory]
    [InlineData(null, "test-key", null, true)]
    [InlineData("ApiKey", "", null, false)]
    [InlineData("AzureCli", "", "16b3c013-d300-468d-ac64-7eda0820b6d3", true)]
    [InlineData("AzureCli", "", "", false)]
    [InlineData("Unsupported", "test-key", null, false)]
    public async Task ModelAuthenticationConfigurationIsExplicit(
        string? authentication, string key, string? tenantId, bool valid)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Model:Endpoint"] = "https://example.openai.azure.com/openai/v1/",
            ["Model:Name"] = "test-model",
            ["Model:Authentication"] = authentication,
            ["Model:ApiKey"] = key,
            ["Model:TenantId"] = tenantId
        });
        builder.AddDemoServices();
        await using var app = builder.Build();
        if (valid)
            Assert.NotNull(app.Services.GetRequiredService<IChatClient>());
        else
            Assert.Throws<ModelConfigurationException>(() => app.Services.GetRequiredService<IChatClient>());
    }

    [Fact]
    public void FixturesHaveFixedClockAndOnlyOneTimelyOption()
    {
        Assert.Equal(14, WeatherTool.GetWeather("Torino").TemperatureC);
        Assert.Throws<ArgumentException>(() => WeatherTool.GetWeather("Roma"));
        Assert.Equal("in ritardo", Scenario.FindOrder("ORD-1042")!.Status);
        Assert.Null(Scenario.FindOrder("missing"));
        var useful = Assert.Single(Scenario.Options("ORD-1042"), o => o.AvailableFrom <= Scenario.Deadline);
        Assert.Equal("OPT-1042-DEPOT", useful.OptionId);
        Assert.Equal(0, useful.Cost);
        Assert.Empty(Scenario.Options("ORD-1043"));
    }

    [Fact]
    public void BasicProjectsDoNotReferenceAdvanceAssemblies()
    {
        foreach (var assembly in new[]
        {
            typeof(BasicProgram).Assembly, typeof(Basic.Orchestrator.McpDemo).Assembly,
            typeof(BasicMcpProgram).Assembly,
            typeof(BasicA2AProgram).Assembly, typeof(Basic.Skill.SkillDemo).Assembly,
            typeof(WeatherTool).Assembly, typeof(DemoHosting).Assembly
        })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(),
                reference => reference.Name!.StartsWith("Advance.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdvanceProtocolsExposeOnlyCommerceCapabilities()
    {
        await using var api = Start<CommerceApiProgram>();
        using var apiHttp = api.CreateClient();
        await using var server = Start<AdvanceMcpProgram>(commerce: apiHttp.BaseAddress);
        using var mcpHttp = server.CreateClient();
        await using var mcp = await McpClient.CreateAsync(new HttpClientTransport(
            new() { Endpoint = new Uri(mcpHttp.BaseAddress!, "/mcp") }, mcpHttp));
        Assert.Equal(["get_delivery_options", "get_order"], (await mcp.ListToolsAsync()).Select(t => t.Name).Order().ToArray());
        await using var specialist = Start<AdvanceA2AProgram>(new ScriptedChatClient(), mcp: mcpHttp.BaseAddress);
        using var http = specialist.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/cards/travel")).StatusCode);
        var card = await http.GetFromJsonAsync<AgentCard>("/cards/logistics");
        Assert.Equal("logistics", card!.Name);
    }

    [Fact]
    public async Task MockApiIsReadOnlyAndReturnsNotFound()
    {
        await using var api = Start<CommerceApiProgram>();
        using var http = api.CreateClient();
        var order = await http.GetFromJsonAsync<Order>("/orders/ORD-1042");
        Assert.Equal(Scenario.Now.AddDays(2).AddHours(3), order!.EstimatedDelivery);
        Assert.Equal(2, (await http.GetFromJsonAsync<DeliveryOption[]>("/delivery-options?orderId=ORD-1042"))!.Length);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/orders/missing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/delivery-options?orderId=missing")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await http.PostAsync("/orders/ORD-1042", null)).StatusCode);
    }

    [Fact]
    public async Task BasicMcpDiscoveryAndAgentToolCallsUseRealHttp()
    {
        await using var server = Start<BasicMcpProgram>();
        using var serverHttp = server.CreateClient();
        await using var mcp = await McpClient.CreateAsync(new HttpClientTransport(
            new() { Endpoint = new Uri(serverHttp.BaseAddress!, "/mcp") }, serverHttp));
        var tools = await mcp.ListToolsAsync();
        Assert.Equal("get_weather", Assert.Single(tools).Name);
        var unknown = await mcp.CallToolAsync("get_weather", new Dictionary<string, object?> { ["city"] = "Roma" });
        Assert.True(unknown.IsError);

        var model = WeatherModel();
        await using var demo = Start<BasicProgram>(model, mcp: serverHttp.BaseAddress);
        using var http = demo.CreateClient();
        const string prompt = "Vorrei conoscere il meteo a Torino per una passeggiata.";
        var response = await RunWithTimings(http, new BasicRequest(prompt, "mcp"));
        Assert.Contains("pioggia", response.Text);
        Assert.Contains(response.Steps, step => step.Name == "mcp.connect");
        Assert.Contains(response.Steps, step => step.Name == "mcp.discover_tools");
        Assert.Equal(2, response.Steps.Count(step => step.Operation == "chat"));
        Assert.Equal(22L, response.InputTokens);
        Assert.Equal(14L, response.OutputTokens);
        Assert.Contains(response.Steps, step => step.Operation == "execute_tool" && step.Name.Contains("get_weather"));
        Assert.Single(model.AdvertisedTools[0]);
        Assert.Contains(model.FirstMessages, m => m.Text == prompt);
        AssertToolFacts(model, false);
    }

    [Fact]
    public async Task BasicA2ACardAndDelegationReachRemoteAgent()
    {
        var specialistModel = WeatherModel();
        await using var specialists = Start<BasicA2AProgram>(specialistModel);
        using var remoteHttp = specialists.CreateClient();
        var card = await remoteHttp.GetFromJsonAsync<AgentCard>("/cards/travel");
        Assert.Equal("travel", card!.Name);
        Assert.Equal(HttpStatusCode.NotFound, (await remoteHttp.GetAsync("/cards/logistics")).StatusCode);
        Assert.Contains("/a2a/", Assert.Single(card.SupportedInterfaces!).Url);
        var mainModel = new ScriptedChatClient(_ => ScriptedChatClient.Answer("Risposta del principale"));
        using var exporter = new RecordingExporter();
        await using var demo = Start<BasicProgram>(mainModel, specialists: remoteHttp.BaseAddress, exporter: exporter);
        using var http = demo.CreateClient();
        const string prompt = "Consigliami come visitare Torino oggi.";
        var response = await RunWithTimings(http, new BasicRequest(prompt, "a2a"));
        Assert.Equal("Risposta del principale", response.Text);
        Assert.Contains(response.Steps, step => step.Name == "a2a.read_card");
        Assert.Contains(response.Steps, step => step.Name == "a2a.remote_agent");
        Assert.Single(response.Steps, step => step.Operation == "chat");
        Assert.Equal(11L, response.InputTokens);
        Assert.Equal(7L, response.OutputTokens);
        Assert.DoesNotContain(response.Steps, step => step.Operation == "execute_tool");
        var remote = Assert.IsType<Basic.Orchestrator.RemoteAgentDetails>(response.RemoteAgent);
        Assert.Equal("travel", remote.Name);
        Assert.Equal(card.Description, remote.Description);
        Assert.Equal(card.Version, remote.Version);
        Assert.Equal(card.SupportedInterfaces[0].Url, remote.Endpoint);
        var remoteExecution = Assert.IsType<ExecutionReport>(remote.Execution);
        Assert.Equal(response.TraceId, remoteExecution.TraceId);
        Assert.Equal(22L, remoteExecution.InputTokens);
        Assert.Equal(14L, remoteExecution.OutputTokens);
        Assert.Equal(33L, response.TotalInputTokens);
        Assert.Equal(21L, response.TotalOutputTokens);
        Assert.Equal(2, remoteExecution.Steps.Count(step => step.Operation == "chat"));
        Assert.Contains(remoteExecution.Steps, step => step.AgentName == "TravelTipsAgent");
        Assert.Contains(remoteExecution.Steps, step => step.Operation == "execute_tool");
        Assert.True(remoteExecution.DurationMs > 0);
        var delegation = Assert.Single(response.Steps, step => step.Name == "a2a.remote_agent");
        Assert.True(remoteExecution.DurationMs <= delegation.DurationMs);
        Assert.DoesNotContain(remoteExecution.Steps, step => step.AgentName == "AssistenteViaggio");
        var distributed = exporter.Spans.Where(span => span.TraceId.ToString() == response.TraceId).ToArray();
        Assert.Contains(distributed, span => Equals(span.GetTagItem("gen_ai.agent.name"), "TravelTipsAgent"));
        Assert.Contains(distributed, span => Equals(span.GetTagItem("gen_ai.operation.name"), "execute_tool") &&
            span.DisplayName.Contains("get_weather"));
        Assert.Contains(distributed, span => Equals(span.GetTagItem("gen_ai.agent.name"), "TravelTipsAgent") &&
            span.GetTagItem("gen_ai.input.messages")?.ToString()?.Contains(prompt) == true &&
            span.GetTagItem("gen_ai.output.messages")?.ToString()?.Contains("pioggia") == true);
        Assert.Contains(distributed, span => Equals(span.GetTagItem("gen_ai.operation.name"), "execute_tool") &&
            span.GetTagItem("gen_ai.tool.call.arguments")?.ToString()?.Contains("Torino") == true &&
            span.GetTagItem("gen_ai.tool.call.result")?.ToString()?.Contains("pioggia") == true);
        Assert.Contains("pioggia", string.Join("\n", mainModel.LastMessages.Select(m => m.Text)));
        Assert.Contains(specialistModel.FirstMessages, m => m.Text == prompt);
        AssertToolFacts(specialistModel, false);
    }

    [Fact]
    public async Task BasicSkillIsDiscoveredThenGuidesThreeLocalToolCalls()
    {
        var model = new ScriptedChatClient(
            options => LoadSkill(options, "city-briefing"),
            _ => ScriptedChatClient.Call("get_weather", new() { ["city"] = "Torino" }),
            _ => ScriptedChatClient.Call("get_packing_list", new() { ["city"] = "Torino" }),
            _ => ScriptedChatClient.Call("get_travel_alerts", new() { ["city"] = "Torino" }),
            _ => ScriptedChatClient.Answer("Briefing dai tre tool locali"));
        await using var demo = Start<BasicProgram>(model);
        using var http = demo.CreateClient();
        const string prompt = "Prepara un briefing per visitare Torino.";
        var response = await RunWithTimings(http, new BasicRequest(prompt, "skill"));
        Assert.Equal(5, response.Steps.Count(step => step.Operation == "chat"));
        Assert.Equal(55L, response.InputTokens);
        Assert.Equal(35L, response.OutputTokens);
        foreach (var tool in new[] { "load_skill", "get_weather", "get_packing_list", "get_travel_alerts" })
            Assert.Contains(response.Steps, step => step.Operation == "execute_tool" && step.Name.Contains(tool));
        var context = string.Join("\n", model.LastMessages.Select(m => m.Text));
        var results = string.Join("\n", model.LastMessages.SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Select(r => JsonSerializer.Serialize(r.Result)));
        Assert.Contains("Cosa portare", context + results);
        var initialContext = model.FirstInstructions + "\n" + string.Join("\n", model.FirstMessages.Select(m => m.Text));
        Assert.Contains("city-briefing", initialContext);
        Assert.DoesNotContain("Cosa portare", initialContext);
        Assert.Contains("load_skill", model.AdvertisedTools[0]);
        Assert.Equal(["get_packing_list", "get_travel_alerts", "get_weather"],
            model.AdvertisedTools[0].Where(name => name.StartsWith("get_", StringComparison.Ordinal)).Order().ToArray());
        var calls = model.LastMessages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToArray();
        Assert.Equal(["load_skill", "get_weather", "get_packing_list", "get_travel_alerts"],
            calls.Select(call => call.Name).ToArray());
        var toolResults = model.LastMessages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToArray();
        Assert.Equal(4, toolResults.Length);
        Assert.Contains("pioggia", JsonSerializer.Serialize(toolResults[1].Result));
        Assert.Contains("ombrello", JsonSerializer.Serialize(toolResults[2].Result));
        Assert.Contains("giacca impermeabile", JsonSerializer.Serialize(toolResults[2].Result));
        Assert.Contains("scivolosi", JsonSerializer.Serialize(toolResults[3].Result));
        Assert.Contains(model.FirstMessages, m => m.Text == prompt);
    }

    [Theory]
    [InlineData(0, 0, 0L, 0L, 11L, 7L)]
    [InlineData(null, 7, null, 14L, null, 21L)]
    [InlineData(11, null, 22L, null, 33L, null)]
    public async Task RemoteA2ATokensPreserveZeroAndMissingCounts(int? input, int? output,
        long? remoteInput, long? remoteOutput, long? totalInput, long? totalOutput)
    {
        var specialistModel = new ScriptedChatClient(
            _ => ScriptedChatClient.Call("get_weather", new() { ["city"] = "Torino" }),
            _ => ScriptedChatClient.Answer("Consigli dello specialista"))
        {
            Usage = new() { InputTokenCount = input, OutputTokenCount = output }
        };
        await using var specialists = Start<BasicA2AProgram>(specialistModel);
        using var remoteHttp = specialists.CreateClient();
        await using var demo = Start<BasicProgram>(
            new ScriptedChatClient(_ => ScriptedChatClient.Answer("Risposta")),
            specialists: remoteHttp.BaseAddress);
        using var http = demo.CreateClient();
        var response = await RunWithTimings(http, new BasicRequest("Torino", "a2a"));
        Assert.Equal(remoteInput, response.RemoteAgent!.Execution!.InputTokens);
        Assert.Equal(remoteOutput, response.RemoteAgent.Execution.OutputTokens);
        Assert.Equal(totalInput, response.TotalInputTokens);
        Assert.Equal(totalOutput, response.TotalOutputTokens);
    }

    [Fact]
    public async Task ConcurrentA2ACallsKeepRemoteMetricsInTheirOwnTrace()
    {
        await using var specialists = Start<BasicA2AProgram>(new ConcurrentSpecialistModel());
        using var remoteHttp = specialists.CreateClient();
        await using var first = Start<BasicProgram>(
            new ScriptedChatClient(_ => ScriptedChatClient.Answer("prima")),
            specialists: remoteHttp.BaseAddress);
        await using var second = Start<BasicProgram>(
            new ScriptedChatClient(_ => ScriptedChatClient.Answer("seconda")),
            specialists: remoteHttp.BaseAddress);
        using var firstHttp = first.CreateClient();
        using var secondHttp = second.CreateClient();
        var results = await Task.WhenAll(
            RunWithTimings(firstHttp, new BasicRequest("prima", "a2a")),
            RunWithTimings(secondHttp, new BasicRequest("seconda", "a2a")));
        Assert.NotEqual(results[0].TraceId, results[1].TraceId);
        Assert.Equal(10L, results[0].RemoteAgent!.Execution!.InputTokens);
        Assert.Equal(20L, results[1].RemoteAgent!.Execution!.InputTokens);
        Assert.Equal(21L, results[0].TotalInputTokens);
        Assert.Equal(31L, results[1].TotalInputTokens);
        foreach (var response in results)
        {
            var remote = response.RemoteAgent!.Execution!;
            Assert.Equal(response.TraceId, remote.TraceId);
            Assert.Single(remote.Steps, step => step.Operation == "chat");
            Assert.DoesNotContain(remote.Steps, step => step.AgentName == "AssistenteViaggio");
        }
    }

    [Fact]
    public void CityBriefingToolsReturnSmallReadOnlyMocks()
    {
        var packing = Basic.Skill.CityBriefingTools.GetPackingList(" torino ");
        Assert.Equal("Torino", packing.City);
        Assert.Equal(["ombrello", "giacca impermeabile"], packing.Items);
        var alerts = Basic.Skill.CityBriefingTools.GetTravelAlerts("TORINO");
        Assert.Equal("Torino", alerts.City);
        Assert.Contains("scivolosi", Assert.Single(alerts.Precautions));
        Assert.Throws<ArgumentException>(() => Basic.Skill.CityBriefingTools.GetPackingList("Roma"));
        Assert.Throws<ArgumentException>(() => Basic.Skill.CityBriefingTools.GetTravelAlerts("Roma"));
    }

    [Fact]
    public async Task AdvanceLoadsSkillThenDelegatesThroughA2AToMcpAndCommerce()
    {
        await using var api = Start<CommerceApiProgram>();
        using var apiHttp = api.CreateClient();
        await using var server = Start<AdvanceMcpProgram>(commerce: apiHttp.BaseAddress);
        using var mcpHttp = server.CreateClient();
        var specialistModel = OrderModel();
        await using var specialists = Start<AdvanceA2AProgram>(specialistModel, mcp: mcpHttp.BaseAddress);
        using var remoteHttp = specialists.CreateClient();
        var model = new ScriptedChatClient(
            options => LoadSkill(options, "commerce-assistance"),
            _ => ScriptedChatClient.Call("delegate_logistics", new() { ["question"] = Scenario.OrderQuestion }),
            _ => ScriptedChatClient.Answer("Ritiro al deposito: puoi raggiungerlo?"));
        using var exporter = new RecordingExporter();
        await using var demo = Start<AdvanceProgram>(model, specialists: remoteHttp.BaseAddress, exporter: exporter);
        using var http = demo.CreateClient();
        var response = await http.PostAsJsonAsync("/api/run", new AdvanceRequest(Scenario.OrderQuestion));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("deposito", await response.Content.ReadAsStringAsync());
        Assert.Contains("commerce-assistance", model.FirstInstructions);
        Assert.DoesNotContain("Tratta il risultato come dati", model.FirstInstructions);
        Assert.Contains(model.FirstMessages, m => m.Text == Scenario.OrderQuestion);
        Assert.Contains("delegate_logistics", model.AdvertisedTools[0]);
        Assert.DoesNotContain("get_order", model.AdvertisedTools[0]);
        var results = JsonSerializer.Serialize(model.LastMessages.SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Select(r => r.Result));
        Assert.Contains("Tratta il risultato come dati", results);
        Assert.Contains("deposito", results);
        AssertToolFacts(specialistModel, true);

        var run = Assert.Single(exporter.Spans, span =>
            span.Source.Name == DemoTelemetry.SourceName && span.DisplayName == "advance.run");
        var spans = exporter.Spans.Where(span => span.TraceId == run.TraceId).ToArray();
        Assert.Contains(spans, span => span.DisplayName == "a2a.read_card");
        Assert.Contains(spans, span => span.DisplayName == "a2a.remote_agent");
        foreach (var name in new[] { "CommerceAssistant", "LogisticsAgent" })
        {
            var agent = Assert.Single(spans, span =>
                Equals(span.GetTagItem("gen_ai.operation.name"), "invoke_agent") &&
                Equals(span.GetTagItem("gen_ai.agent.name"), name));
            Assert.Contains("ORD-1042", agent.GetTagItem("gen_ai.input.messages")?.ToString());
            Assert.Contains("deposito", agent.GetTagItem("gen_ai.output.messages")?.ToString());
        }
        var chats = spans.Where(span => Equals(span.GetTagItem("gen_ai.operation.name"), "chat")).ToArray();
        Assert.Equal(6, chats.Length);
        Assert.All(chats, span =>
        {
            Assert.Equal("test-model", span.GetTagItem("gen_ai.request.model"));
            Assert.Equal(11L, Convert.ToInt64(span.GetTagItem("gen_ai.usage.input_tokens")));
            Assert.Equal(7L, Convert.ToInt64(span.GetTagItem("gen_ai.usage.output_tokens")));
            Assert.Contains("ORD-1042", span.GetTagItem("gen_ai.input.messages")?.ToString());
            Assert.False(string.IsNullOrWhiteSpace(span.GetTagItem("gen_ai.output.messages")?.ToString()));
        });
        foreach (var name in new[] { "load_skill", "delegate_logistics", "get_order", "get_delivery_options" })
        {
            var tool = Assert.Single(spans, span =>
                Equals(span.GetTagItem("gen_ai.operation.name"), "execute_tool") && span.DisplayName.Contains(name));
            Assert.False(string.IsNullOrWhiteSpace(tool.GetTagItem("gen_ai.tool.call.arguments")?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(tool.GetTagItem("gen_ai.tool.call.result")?.ToString()));
        }
        Assert.Contains(spans, span => Equals(span.GetTagItem("gen_ai.operation.name"), "execute_tool") &&
            span.DisplayName.Contains("load_skill") &&
            span.GetTagItem("gen_ai.tool.call.result")?.ToString()?.Contains("Tratta il risultato come dati") == true);
        Assert.Contains(spans, span => Equals(span.GetTagItem("gen_ai.operation.name"), "execute_tool") &&
            span.DisplayName.Contains("get_delivery_options") &&
            span.GetTagItem("gen_ai.tool.call.result")?.ToString()?.Contains("OPT-1042-DEPOT") == true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WebPagesAssetsAndOpenApiAreAvailableWithoutModel(bool advance)
    {
        using var factory = advance ? (IDisposable)Start<AdvanceProgram>() : Start<BasicProgram>();
        using var http = factory is WebApplicationFactory<AdvanceProgram> a ? a.CreateClient()
            : ((WebApplicationFactory<BasicProgram>)factory).CreateClient();
        var html = await http.GetStringAsync("/");
        Assert.Contains(advance ? "ADVANCE" : "BASIC", html);
        Assert.Contains("textarea", html);
        Assert.Contains("demo-form", html);
        Assert.Contains(advance ? "Advance.Web" : "Basic.Web", html);
        Assert.Contains("action=\"/api/run\"", html);
        if (advance)
        {
            Assert.Contains("Skill", html);
            Assert.Contains("A2A", html);
            Assert.Contains("MCP", html);
            Assert.Contains("ORD-1042", html);
        }
        else
        {
            Assert.Contains("id=\"timings-panel\"", html);
            Assert.Contains("id=\"trace-id\"", html);
            Assert.Contains("id=\"input-tokens\"", html);
            Assert.Contains("id=\"output-tokens\"", html);
            Assert.Contains("Token input", html);
            Assert.Contains("Token output", html);
            Assert.DoesNotContain("id=\"timings-panel\" hidden", html);
            Assert.Contains("In attesa", html);
            Assert.Contains("href=\"/tecnologie.html\"", html);
            Assert.Contains("id=\"remote-agent\"", html);
            var technologies = await http.GetStringAsync("/tecnologie.html");
            Assert.Contains("Confronto rapido", technologies);
            Assert.Contains("MCP: uso uno strumento", technologies);
            Assert.Contains("A2A: delego a uno specialista", technologies);
            Assert.Contains("Skill: carico una procedura", technologies);
            Assert.Equal(3, technologies.Split("class=\"technology-diagram\"").Length - 1);
            Assert.Contains("href=\"/#demo-form\"", technologies);
            Assert.DoesNotContain("<script", technologies);
            foreach (var technology in new[] { "mcp", "a2a", "skill" })
                Assert.Contains($"name=\"technology\" value=\"{technology}\"", html);
        }
        Assert.Contains("textContent", await http.GetStringAsync("/demo.js"));
        Assert.Contains("@media", await http.GetStringAsync("/demo.css"));
        var document = await http.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/run", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task EmptyPromptReturns400(string? prompt)
    {
        var model = new ScriptedChatClient();
        await using var basic = Start<BasicProgram>(model);
        await using var advance = Start<AdvanceProgram>(model);
        using var basicHttp = basic.CreateClient();
        using var advanceHttp = advance.CreateClient();
        foreach (var http in new[] { basicHttp, advanceHttp })
        {
            var response = await http.PostAsJsonAsync("/api/run", new { prompt, technology = "mcp" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.ToString());
        }
        Assert.Empty(model.AdvertisedTools);
    }

    [Fact]
    public async Task InvalidTechnologyAndOversizedPromptReturn400()
    {
        var model = new ScriptedChatClient();
        await using var basic = Start<BasicProgram>(model);
        await using var advance = Start<AdvanceProgram>(model);
        using var basicHttp = basic.CreateClient();
        using var advanceHttp = advance.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await basicHttp.PostAsJsonAsync("/api/run", new BasicRequest("Ciao", "invalid"))).StatusCode);
        foreach (var http in new[] { basicHttp, advanceHttp })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await http.PostAsJsonAsync("/api/run", new { prompt = new string('x', 2001), technology = "mcp" })).StatusCode);
        Assert.Empty(model.AdvertisedTools);
    }

    [Fact]
    public async Task MissingModelIsAnExplicit503NotAFakeAnswer()
    {
        await using var app = Start<BasicProgram>();
        using var http = app.CreateClient();
        var response = await http.PostAsJsonAsync("/api/run", new BasicRequest("Prepara un briefing.", "skill"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Modello non configurato", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AiSpansReachConfiguredExporterWithTokensMessagesAndToolContent()
    {
        using var exporter = new RecordingExporter();
        var model = WeatherModel();
        await using var demo = Start<BasicProgram>(model, exporter: exporter);
        using var http = demo.CreateClient();
        var result = await RunWithTimings(http, new BasicRequest("DEMO_PROMPT Torino", "skill"));
        var spans = exporter.Spans.Where(span => span.TraceId.ToString() == result.TraceId).ToArray();
        Assert.Contains(spans, span => span.Source.Name == DemoTelemetry.SourceName && span.DisplayName == "basic.run");
        Assert.Contains(spans, span => Equals(span.GetTagItem("gen_ai.operation.name"), "invoke_agent"));
        var chats = spans.Where(span => Equals(span.GetTagItem("gen_ai.operation.name"), "chat")).ToArray();
        Assert.Equal(2, chats.Length);
        Assert.Equal(22L, result.InputTokens);
        Assert.Equal(14L, result.OutputTokens);
        Assert.All(result.Steps.Where(step => step.Operation == "chat"), step =>
        {
            Assert.Equal(11L, step.InputTokens);
            Assert.Equal(7L, step.OutputTokens);
        });
        Assert.All(result.Steps.Where(step => step.Operation != "chat"), step =>
        {
            Assert.Null(step.InputTokens);
            Assert.Null(step.OutputTokens);
        });
        Assert.All(chats, span =>
        {
            Assert.Equal("test-model", span.GetTagItem("gen_ai.request.model"));
            Assert.Equal(11L, Convert.ToInt64(span.GetTagItem("gen_ai.usage.input_tokens")));
            Assert.Equal(7L, Convert.ToInt64(span.GetTagItem("gen_ai.usage.output_tokens")));
            Assert.Contains("DEMO_PROMPT", span.GetTagItem("gen_ai.input.messages")?.ToString());
            Assert.False(string.IsNullOrWhiteSpace(span.GetTagItem("gen_ai.output.messages")?.ToString()));
        });
        var tool = Assert.Single(spans, span =>
            Equals(span.GetTagItem("gen_ai.operation.name"), "execute_tool"));
        Assert.Contains("Torino", tool.GetTagItem("gen_ai.tool.call.arguments")?.ToString());
        Assert.Contains("pioggia", tool.GetTagItem("gen_ai.tool.call.result")?.ToString());
        var agent = Assert.Single(spans, span =>
            Equals(span.GetTagItem("gen_ai.operation.name"), "invoke_agent"));
        Assert.Contains("DEMO_PROMPT", agent.GetTagItem("gen_ai.input.messages")?.ToString());
        Assert.Contains("pioggia", agent.GetTagItem("gen_ai.output.messages")?.ToString());
    }

    [Fact]
    public async Task TimingCaptureMeasuresElapsedTimeAndIsolatesConcurrentRequests()
    {
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = 0;
        async Task<ExecutionReport> Run(string name)
        {
            using var timings = new ExecutionTimings(name);
            if (Interlocked.Increment(ref ready) == 2)
                bothStarted.SetResult();
            await bothStarted.Task;
            await DemoTelemetry.TraceAsync(name, async () =>
            {
                await Task.Delay(50);
                return true;
            });
            return timings.Complete(name);
        }
        var results = await Task.WhenAll(Task.Run(() => Run("first")), Task.Run(() => Run("second")));
        Assert.NotEqual(results[0].TraceId, results[1].TraceId);
        foreach (var result in results)
        {
            var step = Assert.Single(result.Steps);
            Assert.Equal(result.Text, step.Name);
            Assert.True(step.DurationMs >= 40, $"Measured duration: {step.DurationMs}");
            Assert.True(result.DurationMs >= step.DurationMs);
            Assert.Null(result.InputTokens);
            Assert.Null(result.OutputTokens);
        }
    }

    [Theory]
    [InlineData(0, 0, 0L, 0L)]
    [InlineData(11, 7, 22L, 14L)]
    [InlineData(null, 7, null, 14L)]
    [InlineData(11, null, 22L, null)]
    public void TokenTotalsUseOnlyModelCallsAndPreserveMissingAndZeroCounts(
        int? input, int? output, long? expectedInput, long? expectedOutput)
    {
        using var source = new ActivitySource(DemoTelemetry.AgentSourceName);
        using var timings = new ExecutionTimings("skill");
        using (var agent = source.StartActivity("invoke_agent test"))
        {
            Assert.NotNull(agent);
            agent.SetTag("gen_ai.operation.name", "invoke_agent");
            agent.SetTag("gen_ai.usage.input_tokens", 999);
            agent.SetTag("gen_ai.usage.output_tokens", 999);
            for (var i = 0; i < 2; i++)
            {
                using var chat = source.StartActivity("chat test-model");
                Assert.NotNull(chat);
                chat.SetTag("gen_ai.operation.name", "chat");
                chat.SetTag("gen_ai.usage.input_tokens", input.HasValue ? (double?)input.Value : null);
                chat.SetTag("gen_ai.usage.output_tokens", output.HasValue ? (double?)output.Value : null);
            }
        }
        var result = timings.Complete("Risposta");
        Assert.Equal(expectedInput, result.InputTokens);
        Assert.Equal(expectedOutput, result.OutputTokens);
        var agentStep = Assert.Single(result.Steps, step => step.Operation == "invoke_agent");
        Assert.Null(agentStep.InputTokens);
        Assert.Null(agentStep.OutputTokens);
    }

    [Fact]
    public void MissingUsageInOneModelCallDoesNotProducePartialTotals()
    {
        using var source = new ActivitySource(DemoTelemetry.AgentSourceName);
        using var timings = new ExecutionTimings("mcp");
        using (var first = source.StartActivity("chat first"))
        {
            Assert.NotNull(first);
            first.SetTag("gen_ai.operation.name", "chat");
            first.SetTag("gen_ai.usage.input_tokens", 11);
            first.SetTag("gen_ai.usage.output_tokens", 7);
        }
        using (var second = source.StartActivity("chat second"))
        {
            Assert.NotNull(second);
            second.SetTag("gen_ai.operation.name", "chat");
            second.SetTag("gen_ai.usage.output_tokens", 5);
        }
        var result = timings.Complete("Risposta");
        Assert.Null(result.InputTokens);
        Assert.Equal(12L, result.OutputTokens);
    }

    [Fact]
    public async Task BasicTextApiRemainsCompatibleAndFailedRunsAreMarkedInTraces()
    {
        using var exporter = new RecordingExporter();
        var model = new ScriptedChatClient(
            _ => ScriptedChatClient.Answer("Risposta test"),
            _ => throw new InvalidOperationException("Errore test"));
        await using var app = Start<BasicProgram>(model, exporter: exporter);
        using var http = app.CreateClient();
        using var response = await http.PostAsJsonAsync("/api/run", new BasicRequest("Torino", "skill"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Risposta test", await response.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/run")
        {
            Content = JsonContent.Create(new BasicRequest("Torino", "skill"))
        };
        request.Headers.Accept.ParseAdd("application/json");
        using var failure = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
        Assert.Equal("application/problem+json", failure.Content.Headers.ContentType!.MediaType);
        var problem = await failure.Content.ReadFromJsonAsync<JsonElement>();
        var traceId = problem.GetProperty("traceId").GetString();
        Assert.Matches("^[0-9a-f]{32}$", traceId!);
        Assert.Contains(exporter.Spans, span => span.DisplayName == "basic.run" &&
            span.TraceId.ToString() == traceId &&
            span.Status == ActivityStatusCode.Error &&
            Equals(span.GetTagItem("error.type"), typeof(InvalidOperationException).FullName));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unrelated")]
    public async Task RemoteExecutionHeaderNeverAssociatesInvalidOrUnrelatedMetrics(string scenario)
    {
        using var timings = new ExecutionTimings("a2a");
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (scenario != "missing")
            response.Headers.Add(RemoteExecutionTelemetry.HeaderName, scenario == "malformed" ? "not-json" :
                JsonSerializer.Serialize(new ExecutionReport("", new string('0', 32), 1, [], 11, 7),
                    JsonSerializerOptions.Web));
        using var http = new HttpClient(new RemoteExecutionHandler(NullLogger<RemoteExecutionHandler>.Instance)
        {
            InnerHandler = new FixedResponseHandler(response)
        });
        Task Run() => DemoTelemetry.TraceAsync("a2a.remote_agent", async () =>
        {
            using var received = await http.GetAsync("http://agent.test/a2a/travel/message:send");
            Assert.Null(Activity.Current!.GetCustomProperty(RemoteExecutionTelemetry.ActivityProperty));
            return received.StatusCode;
        });
        if (scenario == "missing")
            await Run();
        else if (scenario == "malformed")
            await Assert.ThrowsAsync<JsonException>(Run);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(Run);
    }

    [Fact]
    public void MissingRemoteMetricsDoNotProduceWebOnlyCombinedTotals()
    {
        var result = new BasicRunResponse("Risposta", "trace", 1, [], 11, 7,
            new Basic.Orchestrator.RemoteAgentDetails("travel", null, null, null, null));
        Assert.Equal(11L, result.InputTokens);
        Assert.Equal(7L, result.OutputTokens);
        Assert.Null(result.TotalInputTokens);
        Assert.Null(result.TotalOutputTokens);
    }

    private static async Task<BasicRunResponse> RunWithTimings(HttpClient http, BasicRequest payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/run")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        var result = await response.Content.ReadFromJsonAsync<BasicRunResponse>();
        Assert.NotNull(result);
        Assert.Matches("^[0-9a-f]{32}$", result.TraceId);
        Assert.True(result.DurationMs > 0);
        Assert.NotEmpty(result.Steps);
        Assert.All(result.Steps, step =>
        {
            Assert.False(step.Failed);
            Assert.True(step.StartMs >= 0);
            Assert.True(step.DurationMs >= 0);
            Assert.True(step.StartMs + step.DurationMs <= result.DurationMs + 1);
        });
        return result;
    }

    private sealed class FixedResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(response);
    }

    private sealed class RecordingExporter : BaseExporter<Activity>
    {
        public ConcurrentQueue<Activity> Spans { get; } = new();

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var span in batch)
                Spans.Enqueue(span);
            return ExportResult.Success;
        }
    }

    private sealed class ConcurrentSpecialistModel : DelegatingChatClient
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public ConcurrentSpecialistModel() : base(new ScriptedChatClient()) { }

        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _started) == 2)
                _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            var prompt = messages.Last(message => message.Role == ChatRole.User).Text;
            return new ChatResponse(ScriptedChatClient.Answer("Consigli"))
            {
                ModelId = "test-model",
                Usage = new() { InputTokenCount = prompt == "prima" ? 10 : 20, OutputTokenCount = 7 }
            };
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var update in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates())
                yield return update;
        }
    }

    private static ChatMessage LoadSkill(ChatOptions options, string name)
    {
        var load = Assert.Single(options.Tools!.OfType<AIFunctionDeclaration>(), t => t.Name == "load_skill");
        var argument = load.JsonSchema.GetProperty("properties").EnumerateObject().First().Name;
        return ScriptedChatClient.Call("load_skill", new() { [argument] = name });
    }

    private static ScriptedChatClient WeatherModel() => new(
        _ => ScriptedChatClient.Call("get_weather", new() { ["city"] = "Torino" }),
        _ => ScriptedChatClient.Answer("Torino: pioggia, 14 C. Porta un ombrello e una giacca."));

    private static ScriptedChatClient OrderModel() => new(
        _ => ScriptedChatClient.Call("get_order", new() { ["orderId"] = "ORD-1042" }),
        _ => ScriptedChatClient.Call("get_delivery_options", new() { ["orderId"] = "ORD-1042" }),
        _ => ScriptedChatClient.Answer("Ritiro gratuito al deposito oggi 14-18 con documento. Puoi raggiungerlo?"));

    private static void AssertToolFacts(ScriptedChatClient model, bool ecommerce)
    {
        var results = model.LastMessages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToArray();
        Assert.Equal(ecommerce ? 2 : 1, results.Length);
        var json = JsonSerializer.Serialize(results.Select(r => r.Result));
        Assert.Contains(ecommerce ? "OPT-1042-DEPOT" : "pioggia", json);
    }

    private static WebApplicationFactory<T> Start<T>(IChatClient? model = null, Uri? commerce = null,
        Uri? mcp = null, Uri? specialists = null, RecordingExporter? exporter = null) where T : class
    {
        var factory = new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Endpoint"] = "", ["Model:Name"] = "", ["Model:ApiKey"] = "",
                ["Model:Authentication"] = "ApiKey", ["Model:ReasoningEffort"] = null,
                ["services:commerce:http:0"] = commerce?.ToString(),
                ["services:mcp-server:http:0"] = mcp?.ToString(),
                ["services:specialists:http:0"] = specialists?.ToString()
            }));
            if (model is not null)
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IChatClient>();
                    services.AddSingleton(model);
                });
            if (exporter is not null)
                builder.ConfigureServices(services => services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(exporter))));
        });
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        return factory;
    }
}
