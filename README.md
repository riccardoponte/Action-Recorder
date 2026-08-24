# 🎙️ Action Recorder

> **Applicazione portatile per registrare ogni flusso di lavoro con cronologia delle azioni e audio**

**Action Recorder** è uno strumento desktop leggero e portatile per Windows progettato per tracciare con precisione chirurgica ogni operazione eseguita dall'utente durante un flusso di lavoro o una sessione operativa. Registra simultaneamente la traccia vocale (microfono) e un log strutturato degli eventi di input e di sistema, esportando i dati in formato JSON standard.

---

## ✨ Caratteristiche Principali

- 🎛️ **Barra Fluttuante Always-on-Top:** Interfaccia scura e compatta, sempre visibile sopra qualsiasi applicazione, con controlli rapidi (*Start*, *Pausa*, *Stop*, *Configurazione Microfono* e contatore eventi in tempo reale).
- 🎙️ **Registrazione Audio PCM & Test Live:**
  - Cattura audio nativa tramite API Windows MultiMedia (`winmm.dll`).
  - Negoziazione automatica della frequenza supportata (44.1 kHz, 48 kHz, 16 kHz).
  - Pannello di test microfono integrato con **VU-Meter / Peak level** in tempo reale per verificare i livelli prima di registrare.
  - Selezione del dispositivo di input o uso automatico del default di sistema (`WAVE_MAPPER`).
- 🖱️ **Tracciamento Avanzato Input & UI Automation:**
  - **Mouse:** Click (singolo, doppio, destro, centrale) con cattura delle coordinate e ispezione dei controlli grafici sottostanti via Microsoft UI Automation (nome controllo, tipo, ID automazione, gerarchia).
  - **Tastiera:** Riconoscimento scorciatoie (es. `Ctrl+C`, `Alt+Tab`) e raggruppamento intelligente dell'input testuale per celle/campi con conferma su `Invio`, `Tab` o click esterno.
  - **Finestre Attive:** Monitoraggio continuo e registrazione automatica del cambio finestra (`window_switch`) con titolo e nome del processo.
- 📁 **Esportazione Pulita:** Ogni sessione genera una cartella dedicata con:
  - `audio.wav`: Traccia audio sincronizzata.
  - `events.json`: Cronologia dettagliata di tutti gli eventi con timestamp, tempo trascorso (`elapsedMs`), dettagli tecnici e metadati UI.
- 🚀 **100% Portatile & Zero Dipendenze Esterne:** Compilabile al volo con il compilatore C# nativo di Windows (`csc.exe`). Non richiede installazioni pesanti o runtime esterni.

---

## 📂 Struttura del Progetto

```text
Action Recorder/
│
├── AVVIA-RECORDER.bat       # Launcher rapido per avviare l'eseguibile
├── bin/
│   └── uia_engine.exe       # Eseguibile nativo compilato
├── src/
│   └── uia_engine.cs        # Codice sorgente C# completo
└── recordings/              # Cartella di output delle sessioni registrate
    └── session_YYYYMMDD_HHMMSS/
        ├── audio.wav        # Audio registrato dal microfono
        └── events.json      # Log strutturato delle azioni
🚀 Come Utilizzarlo
1. Avvio Rapido
Fai doppio click sul file AVVIA-RECORDER.bat (o esegui bin\uia_engine.exe). Apparirà una barra scura in alto a destra sullo schermo.
2. Configurazione Microfono (Opzionale)
Clicca sul pulsante 🎙️ Mic [⚙].
Seleziona il microfono desiderato dal menu a tendina.
Parla nel microfono: la barra del volume mostrerà il livello in tempo reale.
Clicca su Salva e Chiudi.
3. Registrazione del Flusso di Lavoro
Clicca su ● Start: il pallino diventerà rosso (REC) e il contatore inizierà a tracciare gli eventi.
Esegui il tuo lavoro al computer parlando e spiegando i passaggi.
Puoi mettere in ❚❚ Pausa e riprendere in qualsiasi momento.
Premi ■ Stop per terminare la sessione.
Troverai i file pronti nella cartella recordings/session_<data_ora>/.
📋 Esempio di events.json
code
JSON
[
  {
    "index": 1,
    "type": "session_start",
    "timestamp": 1756049045000,
    "elapsedMs": 0,
    "description": "Registrazione avviata dalla barra fluttuante",
    "details": {
      "sessionFolder": "C:\\Users\\...\\recordings\\session_20260824_172405",
      "audioRecording": true,
      "audioFile": "audio.wav",
      "audioFormat": "PCM 16-bit"
    }
  },
  {
    "index": 2,
    "type": "mouse_action",
    "timestamp": 1756049047200,
    "elapsedMs": 2200,
    "description": "Click con tasto sinistro su Button 'Salva con nome' in 'notepad'",
    "details": {
      "action": "click",
      "button": "left",
      "x": 450,
      "y": 320,
      "element": {
        "name": "Salva con nome",
        "controlType": "Button",
        "processName": "notepad"
      }
    }
  }
]
🛠️ Requisiti di Sistema
Sistema Operativo: Windows 10 / Windows 11 (64-bit o 32-bit).
.NET Framework: Versione 4.0 o superiore (già inclusa di default in Windows).
Microfono: Qualsiasi dispositivo integrato, USB, jack o Bluetooth.
