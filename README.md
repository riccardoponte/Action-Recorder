🌐 **Lingua / Language:** [🇮🇹 Italiano](#italiano) · [🇬🇧 English](#english)

---

<a id="italiano"></a>

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

---

<a id="english"></a>

# 🎙️ Action Recorder

> **Portable application to record every workflow with action history and audio**

**Action Recorder** is a lightweight, standalone Windows tool designed to document, map, and automate any desktop workflow. It records user voice and every operating system interaction in parallel, generating a synchronized audio track and a detailed event log.

---

### ⚡ Key Features

- **Always-on-Top Floating Bar:** Minimal interface positioned above every window, freely draggable, with immediate controls (*Start*, *Pause*, *Stop*) and a real-time action counter.
- **Native Audio with Live Test:** Direct voice recording in `.wav` format (PCM 16-bit) via Windows APIs, with microphone selection and a **live VU-Meter** to check audio levels before starting.
- **Advanced UI Automation Inspection:** Tracks not only mouse coordinates, but recognizes UI components of the active application (button names, control types, menus, windows, and processes).
- **Smart Text Grouping:** Detects input in fields and cells (such as Excel or management forms), consolidating typed text upon confirmation (`Enter`, `Tab`, or external click) instead of generating hundreds of individual keystrokes.
- **Ready-to-Use Output:** Each session automatically creates a folder containing the voice track (`audio.wav`) and the structured chronological event log (`events.json`).
- **Zero Dependencies:** No external packages, heavy runtimes, or installation procedures required. Runs immediately on any Windows PC.

---

### 🚦 Usage Workflow

1. **Launch:** Double-click `AVVIA-RECORDER.bat` to open the toolbar in the upper right.
2. **Microphone Test:** Click `🎙️ Mic [⚙]` to test the voice signal and select the correct device.
3. **Recording:** Press `● Start`, perform the procedure on your computer while explaining the steps out loud, then press `■ Stop`.
4. **Collect Files:** Open the session folder in `recordings/` to retrieve `events.json` and `audio.wav`.
5. **Automation Generation (LLM):** Attach both the `.json` file and the `.wav` audio to your preferred LLM (e.g. **Google Gemini**, ChatGPT, Claude) with a prompt such as:
   > *"Transcribe the attached audio and, cross-referencing the spoken explanations with the events in the JSON file, convert this workflow into a PowerShell script (or Python script / Excel Macro) to automate the entire procedure."*
