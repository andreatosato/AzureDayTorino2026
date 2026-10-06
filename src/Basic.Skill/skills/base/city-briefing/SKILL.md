---
name: city-briefing
description: Prepara un briefing di viaggio con meteo, cose da portare e precauzioni usando tre tool locali per Torino.
---

## Procedura

1. Individua la citta richiesta. Se manca, chiedila senza chiamare i tool.
   Il mock supporta solo Torino: per altre citta spiega il limite, senza inventare dati.
2. Chiama `get_weather` con la citta per leggere condizioni e temperatura.
3. Chiama `get_packing_list` con la stessa citta per leggere cosa portare.
4. Chiama `get_travel_alerts` con la stessa citta per leggere le precauzioni.
5. Organizza i risultati in tre sezioni:
   - Meteo: citta, condizioni e temperatura restituite dal tool.
   - Cosa portare: i due oggetti restituiti dal tool.
   - Attenzioni: la precauzione restituita dal tool.

## Limiti

I tool sono funzioni C# locali registrate dall'agente, non server MCP o agenti A2A.
La Skill descrive la procedura: il Markdown non esegue direttamente i tool.
Non sostituire i risultati con dati forniti dall'utente. Se un tool fallisce,
segnala il problema senza completare il briefing con informazioni inventate.
Sono dati sintetici, non previsioni reali o allerte ufficiali.
Non inventare orari di apertura o informazioni sulle attrazioni.
Rispondi brevemente in italiano e indica che e' una demo con dati mockati.
