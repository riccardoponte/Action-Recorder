const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const readline = require('readline');

const ROOT_DIR = path.resolve(__dirname, '..');
const REC_DIR = path.join(ROOT_DIR, 'recordings');
const UIA_EXE = path.join(ROOT_DIR, 'bin', 'uia_engine.exe');

if (!fs.existsSync(REC_DIR)) fs.mkdirSync(REC_DIR, { recursive: true });

if (!fs.existsSync(UIA_EXE)) {
  console.error('\x1b[31m[ERRORE FATALE]\x1b[0m File bin/uia_engine.exe non trovato! Ricompilare lo script.');
  process.exit(1);
}

let sessionFolder = null;
let jsonFile = null;
const events = [];
let isRecording = false;
let startTime = null;

let lastTextBuffer = '';
let lastTextTarget = null;
let textFlushTimer = null;

console.clear();
console.log('\x1b[36m===============================================================\x1b[0m');
console.log('\x1b[36m             ACTION RECORDER — OS & APP TELEMETRY              \x1b[0m');
console.log('\x1b[36m===============================================================\x1b[0m');
console.log(' Barra fluttuante attiva in alto a destra sullo schermo.');
console.log(' Usa i pulsanti sulla barra:');
console.log('   ● Start         -> Avvia la registrazione');
console.log('   ❚❚ Pausa        -> Sospende / Riprende');
console.log('   ■ Stop          -> Salva in events.json e chiude');
console.log('===============================================================\n');

function describeEvent(ev) {
  const type = ev.type;
  const d = ev.data || {};
  const el = d.element || {};

  switch (type) {
    case 'window_switch':
      return `Finestra attiva: '${d.windowTitle || 'Senza Titolo'}' (App: ${d.processName || 'N/D'})`;

    case 'mouse_action': {
      const btn = d.button === 'right' ? 'destro' : (d.button === 'middle' ? 'centrale' : 'sinistro');
      const act = d.action === 'dblclick' ? 'Doppio click' : 'Click';
      if (el && el.controlType) {
        const elName = el.name ? `'${el.name}'` : 'senza nome';
        const hier = el.hierarchy && el.hierarchy.length ? ` [Percorso: ${el.hierarchy.join(' > ')}]` : '';
        const id = el.automationId ? ` (ID: ${el.automationId})` : '';
        return `${act} con tasto ${btn} su ${el.controlType} ${elName}${id} in '${el.processName}'${hier}`;
      }
      return `${act} con tasto ${btn} alle coordinate (X: ${d.x}, Y: ${d.y})`;
    }

    case 'key_action': {
      const mods = [];
      if (d.ctrl) mods.push('Ctrl');
      if (d.shift) mods.push('Shift');
      if (d.alt) mods.push('Alt');
      if (d.win) mods.push('Win');
      const combo = [...mods, d.key].join('+');
      const target = el && el.controlType ? ` nel controllo ${el.controlType} '${el.name || ''}'` : '';
      return `Premuto tasto [${combo}]${target}`;
    }

    default:
      return `${type}`;
  }
}

function flushTextBuffer() {
  if (lastTextBuffer.length > 0 && isRecording) {
    const textEvent = {
      index: events.length + 1,
      type: 'text_entry',
      timestamp: Date.now(),
      elapsedMs: Date.now() - (startTime || Date.now()),
      description: `Inserito testo: "${lastTextBuffer}" in ${lastTextTarget ? lastTextTarget.controlType + ' \'' + (lastTextTarget.name||'') + '\'' : 'controllo attivo'}`,
      details: {
        text: lastTextBuffer,
        target: lastTextTarget
      }
    };
    events.push(textEvent);
    console.log(`\x1b[32m[+${textEvent.elapsedMs}ms]\x1b[0m \x1b[1m[TEXT]\x1b[0m ${textEvent.description}`);
    lastTextBuffer = '';
    lastTextTarget = null;
  }
}

const uiaProc = spawn(UIA_EXE, [], {
  stdio: ['pipe', 'pipe', 'pipe']
});

const rl = readline.createInterface({
  input: uiaProc.stdout,
  terminal: false
});

rl.on('line', (line) => {
  if (!line || !line.trim()) return;
  try {
    const raw = JSON.parse(line.trim());

    if (raw.type === 'session_control') {
      const action = raw.action;
      if (action === 'start') {
        const now = new Date();
        const pad = (n) => String(n).padStart(2, '0');
        const sessionName = `session_${now.getFullYear()}${pad(now.getMonth()+1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
        sessionFolder = path.join(REC_DIR, sessionName);
        fs.mkdirSync(sessionFolder, { recursive: true });
        jsonFile = path.join(sessionFolder, 'events.json');

        events.length = 0;
        isRecording = true;
        startTime = Date.now();
        console.log(`\n\x1b[32m▶ REGISTRAZIONE AVVIATA\x1b[0m -> Cartella: \x1b[33m${sessionFolder}\x1b[0m\n`);
        
        events.push({
          index: 1,
          type: 'session_start',
          timestamp: startTime,
          elapsedMs: 0,
          description: 'Registrazione avviata dalla barra fluttuante'
        });
      } else if (action === 'pause') {
        flushTextBuffer();
        isRecording = false;
        console.log(`\n\x1b[33m❚❚ REGISTRAZIONE IN PAUSA\x1b[0m\n`);
      } else if (action === 'resume') {
        isRecording = true;
        console.log(`\n\x1b[32m▶ REGISTRAZIONE RIPRESA\x1b[0m\n`);
      } else if (action === 'stop') {
        flushTextBuffer();
        isRecording = false;
        if (jsonFile && events.length > 0) {
          fs.writeFileSync(jsonFile, JSON.stringify(events, null, 2), 'utf8');
          console.log(`\n\x1b[36m===============================================================\x1b[0m`);
          console.log(`\x1b[32m Salvati con successo ${events.length} eventi in:\x1b[0m`);
          console.log(`   ${jsonFile}`);
          console.log(`\x1b[36m===============================================================\x1b[0m\n`);
        }
        setTimeout(() => process.exit(0), 400);
      }
      return;
    }

    if (!isRecording) return;

    if (raw.type === 'key_action') {
      const d = raw.data || {};
      if (!d.ctrl && !d.alt && !d.win && d.key && d.key.length === 1) {
        lastTextBuffer += d.key;
        lastTextTarget = d.element;
        clearTimeout(textFlushTimer);
        textFlushTimer = setTimeout(flushTextBuffer, 700);
        return;
      } else {
        flushTextBuffer();
      }
    } else {
      flushTextBuffer();
    }

    const elapsed = raw.timestamp ? (raw.timestamp - startTime) : (Date.now() - startTime);
    const desc = describeEvent(raw);

    const fullEvent = {
      index: events.length + 1,
      type: raw.type,
      timestamp: raw.timestamp || Date.now(),
      elapsedMs: elapsed,
      description: desc,
      details: raw.data
    };

    events.push(fullEvent);

    const tag = raw.type.replace('_action', '').toUpperCase();
    console.log(`\x1b[33m[+${elapsed}ms]\x1b[0m \x1b[35m[${tag}]\x1b[0m ${desc}`);
  } catch (err) { }
});

process.on('SIGINT', () => {
  if (jsonFile && events.length > 0) {
    fs.writeFileSync(jsonFile, JSON.stringify(events, null, 2), 'utf8');
  }
  try { uiaProc.kill(); } catch (_) {}
  process.exit(0);
});