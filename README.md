# MCP server, A2A, Skill, mamma mia che mal di testAI!

**Due demo indipendenti .NET: BASIC e ADVANCE.** Una solution, un unico AppHost Aspire
e Microsoft Agent Framework in C#. Nessuna scrittura, nessun database.

| Demo | Apri nel browser dopo l'avvio Aspire | Contenuto |
| --- | --- | --- |
| **BASIC / Basic.Web** | http://localhost:5074 | Scegli MCP, A2A oppure Skill e scrivi la richiesta |
| **ADVANCE / Advance.Web** | http://localhost:5159 | Commerce: Skill + delega A2A + tool MCP nello stesso percorso |

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
Configurare una volta nell'unico **AppHost**, che mantiene lo stesso UserSecretsId
dei precedenti AppHost per riutilizzare il Foundry gia' scelto.
Le demo non condividono servizi o logica di dominio.

```powershell
dotnet user-secrets --project .\src\AppHost set "Model:Endpoint" "https://ai-1nn0vaai-demo-swc-6a198800.openai.azure.com/openai/v1/"
dotnet user-secrets --project .\src\AppHost set "Model:Name" "gpt-6-sol"
dotnet user-secrets --project .\src\AppHost set "Model:ReasoningEffort" "None"
dotnet user-secrets --project .\src\AppHost set "Model:Authentication" "AzureCli"
dotnet user-secrets --project .\src\AppHost set "Model:TenantId" "16b3c013-d300-468d-ac64-7eda0820b6d3"
```

Il deployment `gpt-6-sol` esiste gia': non viene creato o modificato.
Con GPT-6 SOL, Chat Completions richiede `reasoning_effort=none` per usare
i tool: `Model:ReasoningEffort=None` viene applicato a tutti gli agenti,
anche agli specialisti A2A. Per usare il reasoning con i tool servirebbe
passare alla Responses API. Cambiando modello, rimuovere questa opzione
se non supportata: `dotnet user-secrets --project .\src\AppHost remove "Model:ReasoningEffort"`.
Il token viene ottenuto e rinnovato dal SDK, senza salvarlo nei file del progetto.
Questa modalita' e' pensata per i processi locali Aspire, non per il deployment cloud.

### Alternativa: endpoint con chiave API

Per un'altra risorsa che accetta chiavi:

```powershell
dotnet user-secrets --project .\src\AppHost set "Model:Authentication" "ApiKey"
dotnet user-secrets --project .\src\AppHost set "Model:Endpoint" "https://NOME-RISORSA.openai.azure.com/openai/v1/"
dotnet user-secrets --project .\src\AppHost set "Model:Name" "NOME-DEPLOYMENT"
dotnet user-secrets --project .\src\AppHost set "Model:ApiKey" "LA-TUA-CHIAVE"
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
dotnet run --project .\src\AppHost --launch-profile http
```

Aprire il link di login della dashboard stampato nel terminale.
Tutte le sette app sono visibili ma **nessuna parte automaticamente**:
scegliere **Start** sulla risorsa da eseguire. Le due demo possono girare
separatamente o contemporaneamente nella stessa dashboard.
Avviare anche le dipendenze, in questo ordine:

- **BASIC**: `basic-mcp-server` e `basic-a2a`, poi `basic-web`.
- **ADVANCE**: `advance-mock-api`, `advance-mcp-server`, `advance-a2a`, poi `advance-web`.

Le risorse **basic-web** e **advance-web** sono i due ingressi con UI: aprire
il loro endpoint HTTP (rispettivamente http://localhost:5074 e
http://localhost:5159), non gli endpoint dei server MCP, A2A o del mock.
`Basic.Orchestrator` e `Advance.Commerce` sono librerie, non applicazioni da
avviare. Se Aspire era gia' in esecuzione prima di questa modifica, fermare
la sessione AppHost, ricompilare e riavviarla per vedere i nuovi host web.

Le attese di salute sono mantenute: un'app resta in attesa finche' le proprie
dipendenze non sono avviate e sane. Start non avvia automaticamente le dipendenze.
Usare **Stop** sulla singola risorsa per fermarla.
Il profilo HTTP e' **solo per sviluppo locale su loopback**.

In VS Code: **F5**, configurazione **AppHost - Aspire**.
E' disponibile anche il task **Avvia AppHost**.
F5 avvia Aspire; non e' il Foundry Agent Inspector.

### Debug delle applicazioni e breakpoint

Serve l'estensione **C#** di Microsoft (`ms-dotnettools.csharp`) in VS Code.
La configurazione **AppHost - Aspire** esegue il debug solo dell'AppHost:
non collega automaticamente il debugger ai processi delle applicazioni.
Per fermarsi nei breakpoint delle demo:

1. Compilare in Debug con `dotnet build .\AzureDayTorino2026.slnx -c Debug`.
2. Avviare l'AppHost con F5 o con il task **Avvia AppHost**, poi avviare
   dalla dashboard le risorse necessarie nell'ordine indicato sopra.
3. Impostare i breakpoint nel codice C# dell'applicazione.
4. In **Esegui e debug**, scegliere **Attach .NET - Applicazioni Aspire**
   e avviare il debug con F5. Se l'AppHost e' gia' sotto debug, usare
   **Avvia debug** per aggiungere una sessione senza fermare quella esistente.
5. Selezionare il processo dell'applicazione, non `AppHost`:

   | Risorsa Aspire | Processo/progetto da cercare |
   | --- | --- |
   | basic-web | Basic.Web |
   | basic-mcp-server | Basic.McpServer |
   | basic-a2a | Basic.A2A |
   | advance-web | Advance.Web |
   | advance-mock-api | Advance.MockApi |
   | advance-mcp-server | Advance.McpServer |
   | advance-a2a | Advance.A2A |

   Se il processo e' mostrato come `dotnet`, controllare il percorso della
   DLL nella riga di comando. Il PID e' visibile anche nei dettagli della
   risorsa nella dashboard Aspire.
6. Inviare la richiesta dalla pagina della demo: l'esecuzione si ferma
   quando raggiunge il breakpoint. Usare F10/F11 per avanzare e F5 per
   continuare.

Ripetere l'attach per eseguire il debug di piu' servizi contemporaneamente;
scegliere la sessione attiva nel pannello **Stack di chiamate**. Fermare una
sessione di attach scollega il debugger: per fermare l'app usare **Stop**
nella dashboard. Dopo Stop/Start della risorsa il PID cambia e occorre un
nuovo attach.

L'attach intercetta solo il codice eseguito dopo il collegamento, non
l'inizializzazione gia' completata. Se un breakpoint resta vuoto/non associato,
verificare che il processo sia quello giusto e che DLL e PDB provengano dalla
stessa compilazione Debug del sorgente aperto. Per applicare modifiche al codice,
fermare le risorse interessate, ricompilare, riavviarle e ripetere l'attach.
Durante una pausa i servizi dipendenti possono andare in timeout.

| Demo | Risorsa | URL locale predefinito | Funzione |
| --- | --- | --- | --- |
| BASIC | basic-web | http://localhost:5074 | Basic.Web: UI e dispatch MCP, A2A, Skill |
| BASIC | basic-mcp-server | http://localhost:5296 | Solo get_weather su `/mcp` |
| BASIC | basic-a2a | http://localhost:5131 | Solo agente viaggio A2A |
| ADVANCE | advance-web | http://localhost:5159 | Advance.Web: UI Commerce integrata |
| ADVANCE | advance-mock-api | http://localhost:5297 | Due GET mock |
| ADVANCE | advance-mcp-server | http://localhost:5295 | Solo tool ordini e alternative |
| ADVANCE | advance-a2a | http://localhost:5130 | Solo agente logistico A2A |
| Entrambe | dashboard | http://localhost:15018 | Telemetria di tutte le app avviate |

Le porte sono nei `launchSettings.json`; i client fra servizi usano nomi logici,
non quelle porte. L'AppHost assegna gli alias `mcp-server` e `specialists` alle
risorse della rispettiva demo, senza mescolare i servizi BASIC e ADVANCE.
La risorsa `advance-mock-api` mantiene l'alias client `commerce`.
I nomi nella dashboard identificano i processi; Skill e librerie non sono
risorse separate. Dopo una modifica ai nomi, riavviare l'AppHost.
Se una porta e' occupata, modificarla nel relativo progetto
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
- **Basic.Web**: [Program.cs](src/Basic.Web/Program.cs) riceve la
  richiesta e avvia l'esempio scelto. Il dispatch e' un semplice `switch`:
  usa i client della libreria Orchestrator oppure la libreria Skill,
  senza planner, routing intelligente o pipeline nascosta.

Basic.Web rende i tre concetti eseguibili, **non e' una quarta tecnologia
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
| [AppHost](src/AppHost) | Dashboard unica, selezione manuale delle app BASIC e ADVANCE |
| [Basic.Web](src/Basic.Web) | UI BASIC, API `/api/run` e dispatch esplicito delle tre tecnologie |
| [Basic.Orchestrator](src/Basic.Orchestrator) | Libreria con client MCP/A2A, senza host HTTP |
| [Basic.McpServer](src/Basic.McpServer) | Un tool meteo remoto, senza modello |
| [Basic.A2A](src/Basic.A2A) | Un agente di viaggio remoto con tool locale |
| [Basic.Skill](src/Basic.Skill) | Un provider, una procedura Markdown, nessun server |
| [Basic.Data](src/Basic.Data) | Solo meteo in memoria e istruzioni comuni BASIC |
| [Advance.Web](src/Advance.Web) | UI ADVANCE e API `/api/run` che avvia Commerce |
| [Advance.Commerce](src/Advance.Commerce) | Libreria con Skill Commerce e delega A2A, senza host HTTP |
| [Advance.A2A](src/Advance.A2A) | Solo agente logistico con client MCP |
| [Advance.McpServer](src/Advance.McpServer) | Solo tool ordini e alternative |
| [Advance.MockApi](src/Advance.MockApi) | Due GET con dati sintetici |
| [Advance.Data](src/Advance.Data) | Ordini e alternative in memoria, client del mock |
| [Shared.ServiceDefaults](src/Shared.ServiceDefaults) | Configurazione modello, errori, asset comuni della UI, health check, discovery e telemetria |

I progetti `Demo.*` di produzione sono stati sostituiti. La solution raggruppa
i progetti in **Basic**, **Advance**, **Shared** e **Tests**, con l'**AppHost** unico
alla radice della solution.
L'unico progetto Shared e' plumbing tecnico, non aggiunge concetti da spiegare.
Non contiene dati meteo, dati Commerce o orchestrazione agentica.
I vecchi ingressi per tecnologia sono stati sostituiti: non esistono piu'
`POST /base` e `POST /ecommerce`. Entrambe le UI usano `POST /api/run`
con JSON in ingresso. BASIC richiede `technology` e `prompt`;
ADVANCE richiede `prompt`. Limite: 2000 caratteri, nessuna richiesta vuota.
Senza `Accept: application/json` la risposta resta testo. La UI BASIC richiede
JSON per mostrare anche tempi e token; ADVANCE mantiene la risposta testuale.
La pagina BASIC [MCP, A2A e Skill: differenze e schemi](src/Basic.Web/wwwroot/tecnologie.html),
raggiungibile dal menu su `/tecnologie.html`, confronta protocolli, responsabilita',
confini dei processi e casi d'uso con tre schemi della demo. E' statica e non chiama il modello.
Le chiamate manuali sono in [demo.http](demo.http); OpenAPI e' disponibile su
`/openapi/v1.json` su entrambe le app in Development.

### Tracce AI BASIC/ADVANCE e tempi BASIC

Gli agenti BASIC e ADVANCE, inclusi gli specialisti A2A, usano l'instrumentazione OpenTelemetry
di Agent Framework. ServiceDefaults registra la stessa sorgente nel tracer
e nel meter ed esporta verso l'endpoint OTLP fornito da Aspire.
Non e' necessario aggiungere Application Insights o risorse Azure.

Dopo aver ricompilato e riavviato AppHost e app interessate:

1. Avviare `basic-mcp-server`, `basic-a2a` e `basic-web`.
2. Inviare una richiesta dalla UI BASIC.
3. Leggere **Token e tempi di esecuzione** sotto la risposta, oppure usare
   **Vedi token e tempi**, e copiare il **Trace ID**. Il pannello e' visibile anche
   prima dell'invio, con valori in attesa, e non conserva misure di richieste precedenti.
4. Aprire **Traces** nella dashboard Aspire e cercare il Trace ID. Espandere
   gli span `invoke_agent`, `chat` ed `execute_tool`.

Gli span AI contengono nome agente, modello, durata e token quando il provider
li restituisce. Per entrambe le demo e' abilitato `EnableSensitiveData=true`
su tutti gli agenti, incluso lo specialista A2A: vengono esportati anche
prompt, risposte, argomenti e risultati dei tool. Nel visualizzatore AI Aspire,
aprire **Input e output**; per i singoli scambi espandere gli span `chat`
ed `execute_tool`, oltre allo span complessivo `invoke_agent`.
Le singole chiamate al modello e ai tool, incluso `load_skill`, compaiono
solo se effettivamente eseguite.

**Solo per la demo:** non inserire credenziali, dati personali o informazioni
riservate nelle richieste o nei tool. Il contenuto viene inviato all'exporter
OTLP configurato, non solo mostrato nella UI. Per un uso non dimostrativo
ripristinare `EnableSensitiveData=false`. Dopo una modifica ricompilare,
riavviare le app e inviare una nuova richiesta: le tracce precedenti senza
contenuto non possono essere completate retroattivamente.

In ADVANCE, avviare `advance-mock-api`, `advance-mcp-server`, `advance-a2a`
e `advance-web`, poi inviare una richiesta dalla UI ADVANCE. In Aspire aprire
la traccia del POST `/api/run`: lo span `advance.run` contiene `CommerceAssistant`,
il caricamento `load_skill`, `delegate_logistics`, `a2a.read_card` e
`a2a.remote_agent`. Nello stesso Trace ID lo specialista `LogisticsAgent`
espone le proprie chiamate `chat` e i tool `get_order` e `get_delivery_options`,
con argomenti e risultati sensibili. I servizi MCP e Mock API restano visibili
come chiamate HTTP; non usano un modello proprio. La risposta ADVANCE resta
testuale: le informazioni AI si consultano nella dashboard Aspire.

La UI BASIC mostra tempi server inclusivi a fine richiesta: totale, connessione e
discovery MCP, lettura Agent Card, chiamata A2A remota, agente, chiamate al
modello e tool locali/MCP. Ogni riga include offset iniziale e durata in ms.
I tempi dell'agente includono i sotto-span e **non vanno sommati**.
Il tempo della fase A2A nel Web comprende rete ed esecuzione dello specialista.
Il pannello **Agente A2A remoto** mostra nome dalla Agent Card, nome dell'agente
effettivo, versione, descrizione ed endpoint, insieme a durata, token e operazioni
misurate nel processo dello specialista. Gli offset delle righe remote sono
relativi all'esecuzione remota, non al Web; non sommare durate Web e remote.
Le richieste fallite restano errori ProblemDetails e span in errore, senza
una tabella di tempi di successo; il pannello resta visibile ma segnala misure
non disponibili e la UI elimina i tempi della richiesta precedente.

Il riepilogo mostra durata server e token input/output complessivi, con
conteggi Web separati e, per A2A, conteggi dello specialista remoto.
Le tabelle riportano anche i token di ogni singolo span `chat`.
I totali sommano solo le chiamate al modello, non gli span agente, per evitare
doppi conteggi. Nel percorso A2A i totali includono Web e specialista.
Se un conteggio non viene restituito dal provider, il relativo totale e'
**Non disponibili**, senza stime, somme parziali o sostituzioni con zero.
Per fasi, agenti e tool le colonne token mostrano `-`.

Il contratto JSON BASIC e' `{ text, traceId, durationMs, steps, inputTokens, outputTokens,
remoteAgent, totalInputTokens, totalOutputTokens }`. `inputTokens` e `outputTokens`
restano i conteggi Web per compatibilita'; i campi `total*` includono lo specialista.
`remoteAgent` e' `null` fuori da A2A; contiene `{ name, description, version, endpoint, execution }`.
`execution` contiene le misure remote nello stesso formato del report locale.
Ogni step contiene `{ name, operation, startMs, durationMs, spanId, parentSpanId, failed,
inputTokens, outputTokens, agentName }`.
I conteggi token possono essere `null` quando non disponibili o non applicabili.
Le durate provengono dagli span effettivi, non dal diagramma illustrativo.
Il pannello funziona anche avviando il Web fuori da Aspire; in quel caso
occorre configurare l'exporter OTLP per vedere le tracce nella dashboard.

Le metriche remote sono un'estensione HTTP **della demo**, non campi obbligatori
del protocollo A2A. Basic.A2A restituisce l'header `Demo-Agent-Metrics` sulla stessa
risposta bloccante `/message:send`, senza un secondo invio e senza un archivio globale.
Un collector condiviso misura solo i discendenti della propria richiesta; il client
verifica il Trace ID prima di associare il report. L'header contiene solo nomi,
identificativi, durate e conteggi: nessun prompt o risultato dei tool.
Le risposte streaming non lo espongono, perche' inviano gli header prima del completamento.
Se uno specialista non restituisce l'estensione, il client registra un warning:
la scheda resta visibile, ma le misure remote e i totali complessivi risultano
non disponibili. I conteggi Web rimangono consultabili.

Verifica locale con GPT-6 SOL dell'8 ottobre 2026: tutte e tre le richieste
BASIC completate dal browser, con tabella tempi reale (Skill circa 13,4 s,
MCP 9,0 s, A2A 11,8 s). Nella dashboard Aspire sono stati verificati gli span
agente/modello/tool, gli attributi del deployment e dei token, e lo specialista
A2A nello stesso Trace ID del Web. Sono misure di queste esecuzioni, non benchmark.

### I file da aprire sul palco

| Esempio | File essenziali |
| --- | --- |
| BASIC / MCP | [McpDemo.cs](src/Basic.Orchestrator/McpDemo.cs), [WeatherTools.cs](src/Basic.McpServer/WeatherTools.cs) |
| BASIC / A2A | [A2ADemo.cs](src/Basic.Orchestrator/A2ADemo.cs), [TravelTipsAgent.cs](src/Basic.A2A/TravelTipsAgent.cs) |
| BASIC / Skill | [SkillDemo.cs](src/Basic.Skill/SkillDemo.cs), [SKILL.md](src/Basic.Skill/skills/base/city-briefing/SKILL.md), [CityBriefingTools.cs](src/Basic.Skill/CityBriefingTools.cs) |
| BASIC / Web e dispatch | [Program.cs](src/Basic.Web/Program.cs), [index.html](src/Basic.Web/wwwroot/index.html), [AppHost.cs](src/AppHost/AppHost.cs) |
| ADVANCE / Web | [Program.cs](src/Advance.Web/Program.cs), [index.html](src/Advance.Web/wwwroot/index.html) |
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
Questa verifica precede l'unificazione degli AppHost e non certifica il nuovo avvio manuale.

Verifica del 7 ottobre 2026 sull'AppHost unico: build senza errori o warning,
restore in locked mode riuscito e **6 test mirati superati** (protocolli,
isolamento BASIC/ADVANCE e UI/OpenAPI). Nella dashboard tutte le sette app
risultano inizialmente `Not started`; dopo Start manuale, i sette endpoint
`/health` rispondono HTTP 200. Nessuna inferenza LLM eseguita in questa verifica.

Verifica dell'8 ottobre 2026 con gli host espliciti `Basic.Web` e `Advance.Web`:
build Release senza errori o warning, restore in locked mode e **12 test mirati
superati**, inclusi i tre percorsi BASIC e la catena integrata ADVANCE.
Entrambe le UI, gli asset, gli health check e OpenAPI rispondono HTTP 200;
la pubblicazione include anche le rispettive Skill. Nel browser sono stati
verificati i selettori BASIC e l'invio dei form con visualizzazione dell'errore
di modello assente. Nessuna inferenza LLM reale eseguita in questa verifica.

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

La libreria [Shared.ServiceDefaults](src/Shared.ServiceDefaults) evita di duplicare
la configurazione modello, la gestione degli errori e gli asset comuni della UI,
e contiene anche health check, service discovery e telemetria Aspire.
Non e' un framework di orchestrazione.
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
