# 🎙️ Action Recorder

> **Applicazione portatile per registrare ogni flusso di lavoro con cronologia delle azioni e audio**

**Action Recorder** è uno strumento leggero e autonomo per Windows ideato per documentare, mappare e analizzare qualsiasi processo operativo al computer. Registra in parallelo la voce dell'utente e ogni interazione con il sistema operativo, generando una traccia audio sincronizzata e un log dettagliato degli eventi.

---

### ⚡ Punti di Forza

- **Barra Fluttuante Always-on-Top:** Interfaccia minimale posizionata sopra ogni finestra, trascinabile liberamente, con controlli immediati (*Start*, *Pausa*, *Stop*) e contatore delle azioni in tempo reale.
- **Audio Nativo con Test Live:** Registrazione vocale diretta in formato `.wav` (PCM 16-bit) tramite API Windows, con selezione del microfono e **VU-Meter dal vivo** per verificare la voce prima di iniziare.
- **Ispezione Avanzata UI Automation:** Traccia non solo le coordinate del mouse, ma riconosce i componenti grafici dell'applicazione attiva (nomi di pulsanti, tipo di controllo, menu, finestre e processi).
- **Raggruppamento Intelligente del Testo:** Riconosce l'inserimento nei campi e nelle celle (come Excel o form gestionali), consolidando il testo digitato alla conferma (`Invio`, `Tab` o click esterno) anziché generare centinaia di singoli tasti sparsi.
- **Output Pronto all'Uso:** Ogni sessione crea automaticamente una cartella contenente la traccia vocale (`audio.wav`) e la cronologia temporale strutturata (`events.json`).
- **Zero Dipendenze:** Nessun pacchetto esterno, runtime pesante o procedura d'installazione richiesta. Funziona immediatamente su qualsiasi PC Windows.

---

### 🚦 Utilizzo Rapido

1. **Avvia:** Fai doppio click su `AVVIA-RECORDER.bat` per aprire la barra strumenti in alto a destra.
2. **Verifica il Mic:** Clicca su `🎙️ Mic [⚙]` per testare il segnale vocale e scegliere il microfono.
3. **Registra:** Premi `● Start`, svolgi la tua procedura al computer parlando liberamente, quindi premi `■ Stop`.
4. **Risultato:** Troverai l'audio sincronizzato e il file di log dettagliato all'interno della cartella `recordings/`.
