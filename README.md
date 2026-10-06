# MCP server, A2A, Skill, mamma mia che mal di testAI!

**Due demo indipendenti .NET: BASIC e ADVANCE.** Una solution, due AppHost Aspire
e Microsoft Agent Framework in C#. Nessuna scrittura, nessun database.

| Demo | Apri nel browser dopo l'avvio Aspire | Contenuto |
| --- | --- | --- |
| **BASIC** | http://localhost:5074 | Scegli MCP, A2A oppure Skill e scrivi la richiesta |
| **ADVANCE** | http://localhost:5159 | Commerce: Skill + delega A2A + tool MCP nello stesso percorso |

Ogni pagina permette di inviare la richiesta, attendere il modello e leggere
la risposta o l'errore. Gli esempi sono modificabili. Non e' una chat con
memoria: ogni invio e' indipendente. Le chiamate al modello consumano token.

## 1. Configura il modello

Richiede **.NET 10 SDK**. Per queste demo non servono Docker o una distribuzione
su Azure: tutti gli host sono processi .NET locali.

Serve un modello con **chat completions e tool calling**, raggiungibile tramite
API OpenAI-compatible v1. Per Azure OpenAI usare l'endpoint v1 della risorsa
e il nome del deployment, non l'URL del progetto Foundry.

### Foundry scelto: accesso senza chiavi

Risorsa: **ai-1nn0vaai-demo-swc-6a198800**, sottoscrizione
**MCAPS-1500-andreatosato**, regione Sweden Central. Le chiavi API sono
disabilitate su questa risorsa: le demo usano la sessione **Azure CLI**
tramite Microsoft Entra ID. Non occorre un agente ospitato su Foundry.

Accedere con `az login` se la sessione non e' gia' valida. L'utente deve avere
permessi di inferenza sul modello (ad esempio Cognitive Services OpenAI User).
Configurare una volta in uno dei due AppHost: **Basic.AppHost e Advance.AppHost
condividono lo stesso UserSecretsId** per riutilizzare il Foundry gia' scelto.
Non condividono servizi o logica di dominio.

```powershell
dotnet user-secrets --project .\src\Advance.AppHost set "Model:Endpoint" "https://ai-1nn0vaai-demo-swc-6a198800.openai.azure.com/openai/v1/"
dotnet user-secrets --project .\src\Advance.AppHost set "Model:Name" "gpt-6-sol"
dotnet user-secrets --project .\src\Advance.AppHost set "Model:ReasoningEffort" "None"
dotnet user-secrets --project .\src\Advance.AppHost set "Model:Authentication" "AzureCli"
dotnet user-secrets --project .\src\Advance.AppHost set "Model:TenantId" "16b3c013-d300-468d-ac64-7eda0820b6d3"
```

Il deployment `gpt-6-sol` esiste gia': non viene creato o modificato.
Con GPT-6 SOL, Chat Completions richiede `reasoning_effort=none` per usare
i tool: `Model:ReasoningEffort=None` viene applicato a tutti gli agenti,
anche agli specialisti A2A. Per usare il reasoning con i tool servirebbe
passare alla Responses API. Cambiando modello, rimuovere questa opzione
se non supportata: `dotnet user-secrets --project .\src\Advance.AppHost remove "Model:ReasoningEffort"`.
Il token viene ottenuto e rinnovato dal SDK, senza salvarlo nei file del progetto.
Questa modalita' e' pensata per i processi locali Aspire, non per il deployment cloud.

### Alternativa: endpoint con chiave API

Per un'altra risorsa che accetta chiavi:

```powershell
dotnet user-secrets --project .\src\Advance.AppHost set "Model:Authentication" "ApiKey"
dotnet user-secrets --project .\src\Advance.AppHost set "Model:Endpoint" "https://NOME-RISORSA.openai.azure.com/openai/v1/"
dotnet user-secrets --project .\src\Advance.AppHost set "Model:Name" "NOME-DEPLOYMENT"
dotnet user-secrets --project .\src\Advance.AppHost set "Model:ApiKey" "LA-TUA-CHIAVE"
```

I valori sono **segnaposto**, non configurazioni funzionanti. Non incollare la
chiave nel codice, nei file HTTP o nelle slide. User secrets e' uno strumento
di sviluppo, non un vault cifrato. La chiave e' propagata come parametro
segreto Aspire e non pubblicata nel manifest.

In alternativa usare `Model__Endpoint`, `Model__Name`, `Model__Authentication`,
`Model__TenantId`, `Model__ReasoningEffort` o `Model__ApiKey` come
variabili d'ambiente del processo AppHost. Un endpoint locale compatibile
puo' usare HTTP su loopback; per endpoint remoti e' obbligatorio HTTPS.
Non e' necessario cambiare il codice degli esempi per cambiare endpoint.

**Nessun provider configurato viene indovinato. Nessun modello fake viene
usato nell'applicazione.** L'AppHost segnala subito i parametri mancanti.
I test, invece, non richiedono credenziali.

## 2. Avvia con Aspire

```powershell
dotnet build .\AzureDayTorino2026.slnx
# BASIC: nessuna dipendenza dai progetti Advance.*
dotnet run --project .\src\Basic.AppHost --launch-profile http
# ADVANCE: in un altro terminale, oppure da sola
dotnet run --project .\src\Advance.AppHost --launch-profile http
```

Aprire il link di login della relativa dashboard stampato nel terminale.
Ogni AppHost avvia soltanto la propria demo con service discovery.
Si possono eseguire separatamente oppure contemporaneamente, con porte distinte.
Il profilo HTTP e' **solo per sviluppo locale su loopback**.

In VS Code: **F5**, configurazione **BASIC - Aspire** oppure **ADVANCE - Aspire**.
La configurazione composta **BASIC + ADVANCE** le avvia entrambe.
Sono disponibili i task **Avvia BASIC** e **Avvia ADVANCE**.
F5 avvia Aspire; non e' il Foundry Agent Inspector.

| Demo | Risorsa | URL locale predefinito | Funzione |
| --- | --- | --- | --- |
| BASIC | basic | http://localhost:5074 | Orchestrator e piccola UI delle tecnologie |
| BASIC | mcp-server | http://localhost:5296 | Solo get_weather su `/mcp` |
| BASIC | specialists | http://localhost:5131 | Solo agente viaggio A2A |
| BASIC | dashboard | http://localhost:15018 | Telemetria BASIC |
| ADVANCE | advance | http://localhost:5159 | UI Commerce integrata |
| ADVANCE | commerce | http://localhost:5297 | Due GET mock |
| ADVANCE | mcp-server | http://localhost:5295 | Solo tool ordini e alternative |
| ADVANCE | specialists | http://localhost:5130 | Solo agente logistico A2A |
| ADVANCE | dashboard | http://localhost:15019 | Telemetria ADVANCE |

Le porte sono nei `launchSettings.json`; i client fra servizi usano nomi logici,
non quelle porte. Se una porta e' occupata, modificarla nel relativo progetto
e nei soli comandi client del relatore. La dashboard mostra gli URL effettivi.

## 3. BASIC: un concetto alla volta

Aprire http://localhost:5074, scegliere la tecnologia, modificare la richiesta
e premere **Invia richiesta**:

- **MCP**: l'agente scopre e usa `get_weather` sul server MCP. Il mock conosce
  solo Torino, con pioggia e 14 C.
- **A2A**: l'assistente delega allo specialista di viaggio remoto, che usa
  un tool meteo locale. Non usa MCP o Skill: isola il concetto di delega.
- **Skill**: l'agente carica `city-briefing`, poi usa tre tool C# locali:
  `get_weather`, `get_packing_list` e `get_travel_alerts`. Basta chiedere un
  briefing per Torino, senza fornire il meteo. Il Markdown guida la procedura;
  i tool sono registrati nel codice dell'agente, senza MCP o A2A.
- **Orchestrator**: [Program.cs](src/Basic.Orchestrator/Program.cs) riceve la
  richiesta e avvia l'esempio scelto. Il dispatch e' un semplice `switch`:
  niente planner, routing intelligente o pipeline nascosta.

L'orchestrator rende i tre concetti eseguibili, **non e' una quarta tecnologia
o una quarta chiamata**. Aspire avvia i processi; l'orchestrator esegue il percorso.
I dati BASIC restano piccoli: Torino / pioggia / 14 C, due cose da portare
(ombrello e giacca impermeabile) e una precauzione sui marciapiedi scivolosi.
Sono mock, non previsioni o allerte ufficiali. Nessuna API Commerce,
nessun ordine e nessun database. La Skill chiede la citta' se manca e spiega
il limite per citta' diverse da Torino. La sequenza e' guidata dalle istruzioni
al modello, non imposta da una pipeline C#.

## 4. ADVANCE: Commerce con tutte e tre le tecnologie

Aprire http://localhost:5159 e inviare l'esempio dell'ordine ORD-1042:

```text
Richiesta del cliente
  -> assistente Commerce: carica SKILL.md
  -> delegate_logistics: delega A2A allo specialista logistico
  -> specialista: chiama get_order e get_delivery_options via MCP
  -> API Commerce mock
  -> risposta al cliente
```

La Skill guida la procedura; A2A collega i due agenti; MCP fornisce gli
strumenti allo specialista. Non sono piu' tre implementazioni alternative
di Commerce. Se manca l'ordine, l'assistente puo' chiederlo senza delegare.
I diagrammi della UI spiegano il percorso previsto: **non sono tracce live**.
Per le chiamate effettive usare log e tracce della dashboard Aspire.

### Come sono divisi i progetti

| Progetto | Responsabilita' |
| --- | --- |
| [Basic.AppHost](src/Basic.AppHost) | Avvia soltanto BASIC |
| [Basic.Orchestrator](src/Basic.Orchestrator) | Dispatch esplicito, client MCP/A2A e piccola UI |
| [Basic.McpServer](src/Basic.McpServer) | Un tool meteo remoto, senza modello |
| [Basic.A2A](src/Basic.A2A) | Un agente di viaggio remoto con tool locale |
| [Basic.Skill](src/Basic.Skill) | Un provider, una procedura Markdown, nessun server |
| [Basic.Data](src/Basic.Data) | Solo meteo in memoria e istruzioni comuni BASIC |
| [Advance.AppHost](src/Advance.AppHost) | Avvia soltanto ADVANCE |
| [Advance.Commerce](src/Advance.Commerce) | Pagina ADVANCE, Skill Commerce e delega |
| [Advance.A2A](src/Advance.A2A) | Solo agente logistico con client MCP |
| [Advance.McpServer](src/Advance.McpServer) | Solo tool ordini e alternative |
| [Advance.MockApi](src/Advance.MockApi) | Due GET con dati sintetici |
| [Advance.Data](src/Advance.Data) | Ordini e alternative in memoria, client del mock |
| [Shared.Hosting](src/Shared.Hosting) | Configurazione modello, errori e piccoli asset comuni della UI |
| [Shared.ServiceDefaults](src/Shared.ServiceDefaults) | Health check, discovery e telemetria |

I progetti `Demo.*` di produzione sono stati sostituiti. La solution raggruppa
i progetti in **Basic**, **Advance**, **Shared** e **Tests**.
I due progetti Shared sono plumbing tecnico, non aggiungono concetti da spiegare.
Non contengono dati meteo, dati Commerce o orchestrazione agentica.
I vecchi ingressi per tecnologia sono stati sostituiti: non esistono piu'
`POST /base` e `POST /ecommerce`. Entrambe le UI usano `POST /api/run`
con JSON e restituiscono testo. BASIC richiede `technology` e `prompt`;
ADVANCE richiede `prompt`. Limite: 2000 caratteri, nessuna richiesta vuota.
Le chiamate manuali sono in [demo.http](demo.http); OpenAPI e' disponibile su
`/openapi/v1.json` su entrambe le app in Development.

### I file da aprire sul palco

| Esempio | File essenziali |
| --- | --- |
| BASIC / MCP | [McpDemo.cs](src/Basic.Orchestrator/McpDemo.cs), [WeatherTools.cs](src/Basic.McpServer/WeatherTools.cs) |
| BASIC / A2A | [A2ADemo.cs](src/Basic.Orchestrator/A2ADemo.cs), [TravelTipsAgent.cs](src/Basic.A2A/TravelTipsAgent.cs) |
| BASIC / Skill | [SkillDemo.cs](src/Basic.Skill/SkillDemo.cs), [SKILL.md](src/Basic.Skill/skills/base/city-briefing/SKILL.md), [CityBriefingTools.cs](src/Basic.Skill/CityBriefingTools.cs) |
| BASIC / Orchestrator | [Program.cs](src/Basic.Orchestrator/Program.cs), [AppHost.cs](src/Basic.AppHost/AppHost.cs) |
| ADVANCE | [CommerceDemo.cs](src/Advance.Commerce/CommerceDemo.cs), [SKILL.md](src/Advance.Commerce/skills/advance/commerce-assistance/SKILL.md), [LogisticsAgent.cs](src/Advance.A2A/LogisticsAgent.cs), [Tools.cs](src/Advance.McpServer/Tools.cs) |

Il server MCP BASIC espone un solo tool. BASIC scopre e seleziona il tool meteo;
lo specialista ADVANCE espone al proprio modello solo i due tool di ordine,
le cui chiamate vengono inoltrate al server MCP ADVANCE via SDK MCP. Le card A2A sono
`/cards/travel` e `/cards/logistics`, con endpoint
distinti `/a2a/travel` e `/a2a/logistics`. Le Skill sono file locali caricati
dal provider ufficiale: nessun server Skill e nessun esecutore di script.

### Caso comune

ORD-1042 e' in ritardo. L'orologio sintetico e' il 5 ottobre 2026 alle 09:00
UTC+02:00; la scadenza e' il 6 ottobre alle 09:00. Il ritiro gratuito al
deposito oggi 14:00-18:00 e' un candidato; richiede documento e disponibilita'
del cliente a raggiungerlo. Il punto di ritiro domani alle 16:00 e' troppo tardi.

[Scenario.cs](src/Advance.Data/Scenario.cs) contiene tutti i dati Commerce. ORD-1041 e
ORD-1043 sono extra per le prove. L'API ha solo:

- `GET /orders/{id}`: include tracking e consegna prevista.
- `GET /delivery-options?orderId=...`: alternative, costi, orari e vincoli.

Niente bozze, rimborsi o prenotazioni. Gli errori vengono registrati nei log;
gli ingressi restituiscono ProblemDetails con traceId, non risposte inventate.

## 5. Verifiche

```powershell
dotnet test .\tests\Demo.Tests\Demo.Tests.csproj
```

I test verificano configurazione delle due autenticazioni, fixture, errori del mock, MCP discovery/tool calling,
A2A card/delega, caricamento Skill seguito dai tre tool locali e relativi mock, catena integrata ADVANCE, input non validi,
asset web, documenti OpenAPI e configurazione assente.
Usano server Kestrel e protocolli reali su porte dinamiche, con un
`IChatClient` deterministico **solo nel progetto di test**.

I test automatici non sono una valutazione della qualita' di un LLM.
Le risposte reali possono variare tra esecuzioni.

I test verificano anche che BASIC non referenzi assembly Advance e che
i server MCP e A2A dei due gruppi espongano soltanto le proprie capacita'.

Verifica del 6 ottobre 2026: **21 test superati**, build senza errori o warning
e pubblicazione delle UI con le Skill riuscita. BASIC e ADVANCE sono state
provate separatamente con **GPT-6 SOL** reale, senza avviare l'altra demo.
Verificato anche l'avvio contemporaneo dei due AppHost su porte distinte.

## 6. Versioni e scelte

- .NET 10; Aspire AppHost **13.4.6**.
- Microsoft Agent Framework **1.23.0**.
- Bridge A2A **1.23.0-preview.260928.1**, A2A SDK **1.0.0-preview2**.
- MCP SDK **1.3.0**.
- Microsoft.Extensions.AI.OpenAI **10.10.0**.
- Azure.Identity **1.21.0**, con `AzureCliCredential` per lo sviluppo locale.

**A2A e' prerelease** in questa combinazione. Versioni fissate nei progetti
e dipendenze transitive in `packages.lock.json`. Per verificarle senza
aggiornamenti: `dotnet restore .\AzureDayTorino2026.slnx --locked-mode`.

La libreria [Shared.Hosting](src/Shared.Hosting) evita di duplicare la
configurazione modello e la gestione degli errori; non e' un framework di orchestrazione.
[ServiceDefaults](src/Shared.ServiceDefaults) contiene solo plumbing Aspire.
Non spiegarli riga per riga prima delle demo.

Nella versione scelta il provider Skill puo' pubblicizzare anche i tool
risorsa/script: nel sample non sono presenti script ne' un runner.
Il caricamento del solo Markdown locale e' autorizzato dal codice della demo.

Il costruttore OpenAI per l'autenticazione Entra e' ancora marcato sperimentale
dal SDK: la soppressione `OPENAI001` e' limitata alla creazione del client.

## Presentazione

[PowerPoint del talk](presentazione/MCP-A2A-Skill-AzureDayTorino2026.pptx),
modificabile direttamente in PowerPoint.

Il progetto contiene solo presentazione e demo .NET, con relativi test e
configurazioni. Le UI sono servite da ASP.NET Core con HTML, CSS e JavaScript
del browser: nessun Node.js, npm, bundler o server frontend separato.
