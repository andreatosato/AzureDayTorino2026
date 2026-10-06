const form = document.querySelector("#demo-form");
const promptInput = document.querySelector("#prompt");
const answer = document.querySelector("#answer");
const status = document.querySelector("#status");
const panel = document.querySelector(".response-panel");
const submit = form.querySelector('[type="submit"]');
const initialPrompt = promptInput.value;
const examples = {
  mcp: {
    prompt: "Che tempo fa a Torino?",
    title: "MCP: uso uno strumento",
    description: "L'agente scopre il tool get_weather sul server MCP e lo chiama per leggere il meteo.",
    flow: "Richiesta → agente → tool MCP → meteo",
    note: "Il mock meteo conosce solo Torino: pioggia, 14 °C. Non consulta previsioni reali."
  },
  a2a: {
    prompt: "Dammi due consigli per visitare Torino. Leggi il meteo dal tuo tool.",
    title: "A2A: delego a uno specialista",
    description: "L'assistente legge la Agent Card, contatta l'agente di viaggio via A2A e presenta i suoi consigli.",
    flow: "Richiesta → assistente → A2A → agente di viaggio",
    note: "Lo specialista possiede un tool meteo locale. Questo esempio isola A2A: non usa MCP o Skill."
  },
  skill: {
    prompt: "Prepara un briefing per visitare Torino: meteo, cosa portare e attenzioni.",
    title: "Skill: carico una procedura",
    description: "L'agente carica la Skill city-briefing e segue la procedura: legge meteo, cose da portare e precauzioni con tre tool C# locali.",
    flow: "Richiesta → load_skill → get_weather → get_packing_list → get_travel_alerts → briefing",
    note: "Mock solo Torino: pioggia, 14 °C, ombrello, giacca impermeabile e marciapiedi scivolosi. Nessun MCP, A2A o script."
  }
};
function resetExample() {
  const technology = form.querySelector('[name="technology"]:checked')?.value;
  if (!technology) {
    promptInput.value = initialPrompt;
    return;
  }
  const example = examples[technology];
  promptInput.value = example.prompt;
  document.querySelector("#concept-title").textContent = example.title;
  document.querySelector("#concept-description").textContent = example.description;
  document.querySelector("#flow").textContent = example.flow;
  document.querySelector("#fixture-note").textContent = example.note;
}
form.querySelectorAll('[name="technology"]').forEach(input => input.addEventListener("change", () => {
  resetExample();
  answer.textContent = "Invia la richiesta per provare il percorso selezionato.";
  status.textContent = "Pronto";
  panel.classList.remove("error");
}));
document.querySelector("#sample").addEventListener("click", resetExample);
form.addEventListener("submit", async event => {
  event.preventDefault();
  if (!form.reportValidity()) return;
  const payload = { prompt: promptInput.value };
  const technology = form.querySelector('[name="technology"]:checked')?.value;
  if (technology) payload.technology = technology;
  const controls = [...form.querySelectorAll("button, input, textarea")];
  controls.forEach(control => control.disabled = true);
  panel.classList.remove("error");
  panel.setAttribute("aria-busy", "true");
  submit.textContent = "Richiesta in corso...";
  status.textContent = "In esecuzione";
  answer.textContent = "Attendo il modello e gli strumenti. La richiesta puo' richiedere fino a due minuti.";
  try {
    const response = await fetch(form.action, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(130000)
    });
    if (!response.ok) {
      let message = `HTTP ${response.status}: richiesta non riuscita. Consulta i log Aspire.`;
      if (response.headers.get("content-type")?.includes("json")) {
        const problem = await response.json();
        message = [problem.title, problem.detail, ...Object.values(problem.errors ?? {}).flat(),
          problem.traceId ? `TraceId: ${problem.traceId}` : ""].filter(Boolean).join("\n") || message;
      }
      throw new Error(message);
    }
    answer.textContent = await response.text();
    status.textContent = "Completato";
  } catch (error) {
    panel.classList.add("error");
    status.textContent = "Errore";
    answer.textContent = error.name === "TimeoutError"
      ? "Tempo scaduto. Controlla modello e log Aspire prima di riprovare."
      : error.message;
  } finally {
    panel.setAttribute("aria-busy", "false");
    controls.forEach(control => control.disabled = false);
    submit.textContent = "Invia richiesta →";
  }
});
