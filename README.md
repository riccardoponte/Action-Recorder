# 🎙️ Action Recorder

> **Applicazione portatile per registrare ogni flusso di lavoro con cronologia delle azioni e audio**

**Action Recorder** è uno strumento leggero e autonomo per Windows ideato per documentare, mappare e automatizzare qualsiasi processo operativo al computer. Registra in parallelo la voce dell'utente e ogni interazione con il sistema operativo, generando una traccia audio sincronizzata e un log dettagliato degli eventi.

---

### ⚡ Punti di Forza

- **Barra Fluttuante Always-on-Top:** Interfaccia minimale posizionata sopra ogni finestra, trascinabile liberamente, con controlli immediati (*Start*, *Pausa*, *Stop*) e contatore delle azioni in tempo reale.
- **Audio Nativo con Test Live:** Registrazione vocale diretta in formato `.wav` (PCM 16-bit) tramite API Windows, con selezione del microfono e **VU-Meter dal vivo** per verificare la voce prima di iniziare.
- **Ispezione Avanzata UI Automation:** Traccia non solo le coordinate del mouse, ma riconosce i componenti grafici dell'applicazione attiva (nomi di pulsanti, tipo di controllo, menu, finestre e processi).
- **Raggruppamento Intelligente del Testo:** Riconosce l'inserimento nei campi e nelle celle (come Excel o form gestionali), consolidando il testo digitato alla conferma (`Invio`, `Tab` o click esterno) anziché generare centinaia di singoli tasti sparsi.
- **Output Pronto all'Uso:** Ogni sessione crea automaticamente una cartella contenente la traccia vocale (`audio.wav`) e la cronologia temporale strutturata (`events.json`).
- **Zero Dipendenze:** Nessun pacchetto esterno, runtime pesante o procedura d'installazione richiesta. Funziona immediatamente su qualsiasi PC Windows.

---

### 🚦 Flusso di Utilizzo

1. **Avvio:** Fai doppio click su `AVVIA-RECORDER.bat` per aprire la barra degli strumenti in alto a destra.
2. **Test Microfono:** Clicca su `🎙️ Mic [⚙]` per testare il segnale vocale e selezionare la periferica corretta.
3. **Registrazione:** Premi `● Start`, esegui la procedura al computer spiegando a voce i passaggi, quindi premi `■ Stop`.
4. **Raccolta File:** Apri la cartella della sessione in `recordings/` per prelevare `events.json` e `audio.wav`.
5. **Generazione Automazione (LLM):** Allega sia il file `.json` sia l'audio `.wav` al tuo LLM di riferimento (es. **Google Gemini**, ChatGPT, Claude) con un prompt del tipo:
   > *"Trascrivi l'audio allegato e, incrociando le spiegazioni vocali con gli eventi del file JSON, converti questo flusso di lavoro in uno script PowerShell (o script Python / Macro Excel) per automatizzare l'intera procedura."*
