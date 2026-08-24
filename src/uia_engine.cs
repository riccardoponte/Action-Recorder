using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace ActionRecorderEngine
{
    // ==========================================
    // MOTORE AUDIO WINMM NATIVO ROBUSTO
    // ==========================================
    class WaveAudioEngine
    {
        [DllImport("winmm.dll")]
        public static extern int waveInGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        public static extern int waveInGetDevCaps(IntPtr uDeviceID, ref WAVEINCAPS pwic, int cbwic);

        [DllImport("winmm.dll")]
        public static extern int waveInOpen(out IntPtr phwi, int uDeviceID, ref WAVEFORMATEX pwfx, WaveInProc dwCallback, IntPtr dwCallbackInstance, int fdwOpen);

        [DllImport("winmm.dll")]
        public static extern int waveInPrepareHeader(IntPtr hwi, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll")]
        public static extern int waveInUnprepareHeader(IntPtr hwi, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll")]
        public static extern int waveInAddBuffer(IntPtr hwi, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll")]
        public static extern int waveInStart(IntPtr hwi);

        [DllImport("winmm.dll")]
        public static extern int waveInStop(IntPtr hwi);

        [DllImport("winmm.dll")]
        public static extern int waveInReset(IntPtr hwi);

        [DllImport("winmm.dll")]
        public static extern int waveInClose(IntPtr hwi);

        public delegate void WaveInProc(IntPtr hwi, int uMsg, IntPtr dwInstance, ref WAVEHDR pwh, IntPtr dwParam2);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WAVEINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        private const int WMM_DATA = 0x3C0;
        private const int CALLBACK_FUNCTION = 0x30000;
        private const int BUFFER_SIZE = 4096;
        private const int NUM_BUFFERS = 4;
        public const int WAVE_MAPPER = -1;

        private IntPtr _hWaveIn = IntPtr.Zero;
        private WaveInProc _callback;
        private WAVEHDR[] _headers;
        private GCHandle[] _bufferHandles;
        private FileStream _fileStream = null;
        private readonly object _lock = new object();

        private uint _totalDataBytes = 0;
        private bool _isRecording = false;
        private bool _isPaused = false;

        private uint _currentSampleRate = 44100;
        private ushort _currentChannels = 1;

        public event Action<int> OnLevelChanged;

        public static List<KeyValuePair<int, string>> GetInputDevices()
        {
            var list = new List<KeyValuePair<int, string>>();
            list.Add(new KeyValuePair<int, string>(WAVE_MAPPER, "Microfono Predefinito (Windows Default)"));

            int num = waveInGetNumDevs();
            for (int i = 0; i < num; i++)
            {
                WAVEINCAPS caps = new WAVEINCAPS();
                waveInGetDevCaps(new IntPtr(i), ref caps, Marshal.SizeOf(caps));
                list.Add(new KeyValuePair<int, string>(i, caps.szPname));
            }
            return list;
        }

        public bool Start(int deviceId, string filePath)
        {
            Stop();

            _totalDataBytes = 0;
            _isRecording = !string.IsNullOrEmpty(filePath);
            _isPaused = false;

            // Formati supportati da provare in ordine di priorità
            uint[] sampleRates = new uint[] { 44100, 48000, 16000, 22050 };
            ushort[] channelCounts = new ushort[] { 1, 2 };

            _callback = new WaveInProc(DataReceived);
            bool opened = false;
            WAVEFORMATEX chosenWfx = new WAVEFORMATEX();

            foreach (var sr in sampleRates)
            {
                foreach (var ch in channelCounts)
                {
                    WAVEFORMATEX wfx = new WAVEFORMATEX();
                    wfx.wFormatTag = 1; // PCM
                    wfx.nChannels = ch;
                    wfx.nSamplesPerSec = sr;
                    wfx.wBitsPerSample = 16;
                    wfx.nBlockAlign = (ushort)(wfx.nChannels * (wfx.wBitsPerSample / 8));
                    wfx.nAvgBytesPerSec = wfx.nSamplesPerSec * wfx.nBlockAlign;
                    wfx.cbSize = 0;

                    int res = waveInOpen(out _hWaveIn, deviceId, ref wfx, _callback, IntPtr.Zero, CALLBACK_FUNCTION);
                    if (res == 0)
                    {
                        chosenWfx = wfx;
                        _currentSampleRate = sr;
                        _currentChannels = ch;
                        opened = true;
                        break;
                    }
                }
                if (opened) break;
            }

            if (!opened) return false;

            lock (_lock)
            {
                if (_isRecording)
                {
                    _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    byte[] dummyHeader = new byte[44];
                    _fileStream.Write(dummyHeader, 0, 44);
                }
            }

            _headers = new WAVEHDR[NUM_BUFFERS];
            _bufferHandles = new GCHandle[NUM_BUFFERS];

            for (int i = 0; i < NUM_BUFFERS; i++)
            {
                byte[] buf = new byte[BUFFER_SIZE];
                _bufferHandles[i] = GCHandle.Alloc(buf, GCHandleType.Pinned);
                _headers[i] = new WAVEHDR();
                _headers[i].lpData = _bufferHandles[i].AddrOfPinnedObject();
                _headers[i].dwBufferLength = BUFFER_SIZE;

                waveInPrepareHeader(_hWaveIn, ref _headers[i], Marshal.SizeOf(_headers[i]));
                waveInAddBuffer(_hWaveIn, ref _headers[i], Marshal.SizeOf(_headers[i]));
            }

            waveInStart(_hWaveIn);
            return true;
        }

        public void Pause() { _isPaused = true; }
        public void Resume() { _isPaused = false; }

        public void Stop()
        {
            if (_hWaveIn != IntPtr.Zero)
            {
                waveInStop(_hWaveIn);
                waveInReset(_hWaveIn);

                if (_headers != null)
                {
                    for (int i = 0; i < _headers.Length; i++)
                    {
                        waveInUnprepareHeader(_hWaveIn, ref _headers[i], Marshal.SizeOf(_headers[i]));
                        if (_bufferHandles != null && _bufferHandles[i].IsAllocated)
                            _bufferHandles[i].Free();
                    }
                    _headers = null;
                }

                waveInClose(_hWaveIn);
                _hWaveIn = IntPtr.Zero;
            }

            lock (_lock)
            {
                if (_fileStream != null)
                {
                    if (_totalDataBytes > 0)
                    {
                        _fileStream.Seek(0, SeekOrigin.Begin);
                        WriteWavHeader(_fileStream, _totalDataBytes, _currentSampleRate, _currentChannels, 16);
                        _fileStream.Flush();
                    }
                    _fileStream.Close();
                    _fileStream.Dispose();
                    _fileStream = null;
                }
            }

            _isRecording = false;
            _isPaused = false;
        }

        private void DataReceived(IntPtr hwi, int uMsg, IntPtr dwInstance, ref WAVEHDR pwh, IntPtr dwParam2)
        {
            if (uMsg == WMM_DATA && pwh.dwBytesRecorded > 0)
            {
                byte[] data = new byte[pwh.dwBytesRecorded];
                Marshal.Copy(pwh.lpData, data, 0, (int)pwh.dwBytesRecorded);

                lock (_lock)
                {
                    if (_isRecording && !_isPaused && _fileStream != null)
                    {
                        _fileStream.Write(data, 0, data.Length);
                        _totalDataBytes += pwh.dwBytesRecorded;
                    }
                }

                int max = 0;
                for (int i = 0; i < data.Length - 1; i += 2)
                {
                    short sample = (short)(data[i] | (data[i + 1] << 8));
                    int abs = Math.Abs((int)sample);
                    if (abs > max) max = abs;
                }

                int levelPercent = Math.Min(100, (max * 100) / 32767);
                if (OnLevelChanged != null) OnLevelChanged(levelPercent);

                if (_hWaveIn != IntPtr.Zero)
                {
                    waveInAddBuffer(_hWaveIn, ref pwh, Marshal.SizeOf(pwh));
                }
            }
        }

        private static void WriteWavHeader(FileStream fs, uint dataLength, uint sampleRate, ushort channels, ushort bitsPerSample)
        {
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write((uint)(dataLength + 36));
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write((uint)16);
            bw.Write((ushort)1);
            bw.Write(channels);
            bw.Write(sampleRate);
            uint byteRate = (uint)(sampleRate * channels * (bitsPerSample / 8));
            bw.Write(byteRate);
            ushort blockAlign = (ushort)(channels * (bitsPerSample / 8));
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataLength);
        }
    }

    // ==========================================
    // FINESTRA CONFIGURAZIONE E TEST MICROFONO
    // ==========================================
    class MicConfigForm : Form
    {
        private ComboBox cbDevices;
        private ProgressBar pbLevel;
        private Label lblLevel;
        private Button btnToggle;
        private Button btnClose;
        private WaveAudioEngine _testEngine;
        private List<KeyValuePair<int, string>> _devices;

        public int SelectedDeviceId { get; private set; }
        public bool IsMicEnabled { get; private set; }

        public MicConfigForm(int currentDevId, bool isMicOn)
        {
            this.SelectedDeviceId = currentDevId;
            this.IsMicEnabled = isMicOn;

            this.Text = "Configurazione & Test Microfono";
            this.Size = new Size(440, 270);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.ForeColor = Color.White;
            this.TopMost = true;

            Label lblTitle = new Label();
            lblTitle.Text = "Seleziona Dispositivo Audio:";
            lblTitle.Location = new Point(20, 20);
            lblTitle.Size = new Size(220, 20);
            lblTitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.Controls.Add(lblTitle);

            cbDevices = new ComboBox();
            cbDevices.Location = new Point(20, 45);
            cbDevices.Size = new Size(380, 26);
            cbDevices.DropDownStyle = ComboBoxStyle.DropDownList;
            cbDevices.BackColor = Color.FromArgb(40, 40, 52);
            cbDevices.ForeColor = Color.White;

            _devices = WaveAudioEngine.GetInputDevices();
            int selectedIdx = 0;
            for (int i = 0; i < _devices.Count; i++)
            {
                cbDevices.Items.Add(_devices[i].Value);
                if (_devices[i].Key == currentDevId) selectedIdx = i;
            }
            cbDevices.SelectedIndex = selectedIdx;
            cbDevices.SelectedIndexChanged += (s, e) => RestartTestAudio();
            this.Controls.Add(cbDevices);

            Label lblMeter = new Label();
            lblMeter.Text = "Segnale Vocale dal Vivo (Parla nel microfono):";
            lblMeter.Location = new Point(20, 90);
            lblMeter.Size = new Size(380, 20);
            lblMeter.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            this.Controls.Add(lblMeter);

            pbLevel = new ProgressBar();
            pbLevel.Location = new Point(20, 115);
            pbLevel.Size = new Size(320, 22);
            pbLevel.Minimum = 0;
            pbLevel.Maximum = 100;
            this.Controls.Add(pbLevel);

            lblLevel = new Label();
            lblLevel.Text = "0%";
            lblLevel.Location = new Point(348, 117);
            lblLevel.Size = new Size(50, 20);
            lblLevel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            this.Controls.Add(lblLevel);

            btnToggle = new Button();
            btnToggle.Location = new Point(20, 165);
            btnToggle.Size = new Size(180, 38);
            btnToggle.FlatStyle = FlatStyle.Flat;
            btnToggle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnToggle.Cursor = Cursors.Hand;
            UpdateToggleButton();
            btnToggle.Click += (s, e) => {
                IsMicEnabled = !IsMicEnabled;
                UpdateToggleButton();
            };
            this.Controls.Add(btnToggle);

            btnClose = new Button();
            btnClose.Text = "Salva e Chiudi";
            btnClose.Location = new Point(220, 165);
            btnClose.Size = new Size(180, 38);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.BackColor = Color.FromArgb(0, 150, 255);
            btnClose.ForeColor = Color.White;
            btnClose.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => {
                if (cbDevices.SelectedIndex >= 0 && cbDevices.SelectedIndex < _devices.Count)
                    this.SelectedDeviceId = _devices[cbDevices.SelectedIndex].Key;
                this.Close();
            };
            this.Controls.Add(btnClose);

            this.FormClosing += (s, e) => {
                if (_testEngine != null)
                {
                    _testEngine.Stop();
                    _testEngine = null;
                }
            };

            this.Shown += (s, e) => RestartTestAudio();
        }

        private void UpdateToggleButton()
        {
            if (IsMicEnabled)
            {
                btnToggle.Text = "🎙️ Microfono: ATTIVO";
                btnToggle.BackColor = Color.FromArgb(39, 174, 96);
                btnToggle.ForeColor = Color.White;
            }
            else
            {
                btnToggle.Text = "🔇 Microfono: DISATTIVATO";
                btnToggle.BackColor = Color.FromArgb(70, 70, 85);
                btnToggle.ForeColor = Color.FromArgb(200, 200, 200);
            }
        }

        private void RestartTestAudio()
        {
            if (_testEngine != null)
            {
                _testEngine.Stop();
                _testEngine = null;
            }

            int devId = WaveAudioEngine.WAVE_MAPPER;
            if (cbDevices.SelectedIndex >= 0 && cbDevices.SelectedIndex < _devices.Count)
            {
                devId = _devices[cbDevices.SelectedIndex].Key;
            }

            _testEngine = new WaveAudioEngine();
            _testEngine.OnLevelChanged += (lvl) => {
                if (!this.IsDisposed && this.InvokeRequired)
                {
                    this.BeginInvoke((MethodInvoker)delegate {
                        pbLevel.Value = Math.Max(0, Math.Min(100, lvl));
                        lblLevel.Text = lvl + "%";
                    });
                }
            };
            _testEngine.Start(devId, null);
        }
    }

    // ==========================================
    // BARRA VISIVA FLUTTUANTE
    // ==========================================
    class FloatingToolbar : Form
    {
        private Button btnStart;
        private Button btnPause;
        private Button btnStop;
        private Button btnMic;
        private Label lblStatusDot;
        private Label lblStatusText;
        private Label lblCount;

        public bool IsRecording { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsMicEnabled { get; set; }
        public int SelectedMicDeviceId { get; set; }

        public event Action OnStartClicked;
        public event Action OnPauseClicked;
        public event Action OnResumeClicked;
        public event Action OnStopClicked;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public FloatingToolbar()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(515, 52);
            this.BackColor = Color.FromArgb(22, 22, 30);
            this.IsMicEnabled = true;
            this.SelectedMicDeviceId = WaveAudioEngine.WAVE_MAPPER;

            this.Paint += new PaintEventHandler(FormPaint);

            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(workArea.Right - 535, workArea.Top + 20);

            this.MouseDown += new MouseEventHandler(OnDragMouseDown);

            lblStatusDot = new Label();
            lblStatusDot.Text = "●";
            lblStatusDot.ForeColor = Color.Gray;
            lblStatusDot.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            lblStatusDot.Size = new Size(20, 26);
            lblStatusDot.Location = new Point(12, 13);
            lblStatusDot.MouseDown += new MouseEventHandler(OnDragMouseDown);
            this.Controls.Add(lblStatusDot);

            lblStatusText = new Label();
            lblStatusText.Text = "PRONTO";
            lblStatusText.ForeColor = Color.FromArgb(200, 200, 210);
            lblStatusText.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblStatusText.Size = new Size(64, 20);
            lblStatusText.Location = new Point(34, 16);
            lblStatusText.MouseDown += new MouseEventHandler(OnDragMouseDown);
            this.Controls.Add(lblStatusText);

            btnStart = CreateButton("● Start", new Point(102, 10), new Size(74, 32), Color.FromArgb(192, 41, 43), Color.White);
            btnStart.Click += new EventHandler(BtnStart_Click);
            this.Controls.Add(btnStart);

            btnPause = CreateButton("❚❚ Pausa", new Point(182, 10), new Size(78, 32), Color.FromArgb(45, 45, 58), Color.White);
            btnPause.Enabled = false;
            btnPause.Click += new EventHandler(BtnPause_Click);
            this.Controls.Add(btnPause);

            btnStop = CreateButton("■ Stop", new Point(266, 10), new Size(70, 32), Color.FromArgb(45, 45, 58), Color.White);
            btnStop.Enabled = false;
            btnStop.Click += new EventHandler(BtnStop_Click);
            this.Controls.Add(btnStop);

            btnMic = CreateButton("🎙️ Mic [⚙]", new Point(342, 10), new Size(92, 32), Color.FromArgb(39, 174, 96), Color.White);
            btnMic.Click += new EventHandler(BtnMic_Click);
            this.Controls.Add(btnMic);

            lblCount = new Label();
            lblCount.Text = "0 ev";
            lblCount.ForeColor = Color.FromArgb(130, 200, 255);
            lblCount.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblCount.Size = new Size(68, 20);
            lblCount.TextAlign = ContentAlignment.MiddleRight;
            lblCount.Location = new Point(440, 16);
            lblCount.MouseDown += new MouseEventHandler(OnDragMouseDown);
            this.Controls.Add(lblCount);
        }

        private void FormPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(0, 150, 255), 2))
            {
                e.Graphics.DrawRectangle(pen, 1, 1, this.Width - 2, this.Height - 2);
            }
        }

        private void BtnMic_Click(object sender, EventArgs e)
        {
            if (IsRecording) return;
            MicConfigForm cfg = new MicConfigForm(this.SelectedMicDeviceId, this.IsMicEnabled);
            cfg.ShowDialog(this);
            this.SelectedMicDeviceId = cfg.SelectedDeviceId;
            this.IsMicEnabled = cfg.IsMicEnabled;

            if (IsMicEnabled)
            {
                btnMic.Text = "🎙️ Mic [⚙]";
                btnMic.BackColor = Color.FromArgb(39, 174, 96);
            }
            else
            {
                btnMic.Text = "🔇 Mic: OFF";
                btnMic.BackColor = Color.FromArgb(60, 60, 75);
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (!IsRecording)
            {
                IsRecording = true;
                IsPaused = false;
                UpdateUiState();
                if (OnStartClicked != null) OnStartClicked();
            }
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            if (!IsRecording) return;
            IsPaused = !IsPaused;
            UpdateUiState();
            if (IsPaused)
            {
                if (OnPauseClicked != null) OnPauseClicked();
            }
            else
            {
                if (OnResumeClicked != null) OnResumeClicked();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            IsRecording = false;
            IsPaused = false;
            UpdateUiState();
            if (OnStopClicked != null) OnStopClicked();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            this.BringToFront();
            this.Activate();
        }

        private Button CreateButton(string text, Point loc, Size size, Color bg, Color fg)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = loc;
            btn.Size = size;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bg;
            btn.ForeColor = fg;
            btn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private void OnDragMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 2, 0);
            }
        }

        private void UpdateUiState()
        {
            if (IsRecording && !IsPaused)
            {
                lblStatusDot.ForeColor = Color.FromArgb(231, 76, 60);
                lblStatusText.Text = "REC";
                lblStatusText.ForeColor = Color.FromArgb(231, 76, 60);
                btnStart.Enabled = false;
                btnStart.BackColor = Color.FromArgb(35, 35, 45);
                btnPause.Enabled = true;
                btnPause.Text = "❚❚ Pausa";
                btnPause.BackColor = Color.FromArgb(45, 45, 58);
                btnStop.Enabled = true;
                btnStop.BackColor = Color.FromArgb(192, 41, 43);
                btnMic.Enabled = false;
            }
            else if (IsRecording && IsPaused)
            {
                lblStatusDot.ForeColor = Color.FromArgb(241, 196, 15);
                lblStatusText.Text = "PAUSA";
                lblStatusText.ForeColor = Color.FromArgb(241, 196, 15);
                btnPause.Text = "▶ Riprendi";
                btnPause.BackColor = Color.FromArgb(243, 156, 18);
            }
            else
            {
                lblStatusDot.ForeColor = Color.FromArgb(46, 204, 113);
                lblStatusText.Text = "SALVATO";
                lblStatusText.ForeColor = Color.FromArgb(46, 204, 113);
                btnStart.Enabled = false;
                btnPause.Enabled = false;
                btnStop.Enabled = false;
                btnMic.Enabled = false;
            }
        }

        public void SetEventCount(int count)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate {
                    lblCount.Text = count.ToString() + " ev";
                });
            }
            else
            {
                lblCount.Text = count.ToString() + " ev";
            }
        }
    }

    class Program
    {
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static LowLevelMouseProc _mouseProc = MouseHookCallback;
        private static LowLevelKeyboardProc _kbProc = KeyboardHookCallback;
        private static IntPtr _mouseHookId = IntPtr.Zero;
        private static IntPtr _kbHookId = IntPtr.Zero;

        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_LBUTTONDBLCLK = 0x0203;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private static IntPtr _lastForegroundWindow = IntPtr.Zero;
        private static string _lastWindowTitle = "";
        private static string _lastProcessName = "";

        private static FloatingToolbar _ui;
        private static int _currentPid;

        private static List<Dictionary<string, object>> _events = new List<Dictionary<string, object>>();
        private static long _startTime = 0;
        private static string _sessionFolder = "";
        private static string _jsonPath = "";
        private static string _audioPath = "";

        private static WaveAudioEngine _audioEngine = new WaveAudioEngine();

        private static StringBuilder _cellBuffer = new StringBuilder();
        private static Dictionary<string, object> _activeElementInfo = null;
        private static string _activeCellName = "";
        private static string _activeApp = "";
        private static bool _inEditSession = false;

        [DllImport("user32.dll")]
        private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [STAThread]
        static void Main(string[] args)
        {
            _currentPid = Process.GetCurrentProcess().Id;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _ui = new FloatingToolbar();

            _ui.OnStartClicked += StartRecording;
            _ui.OnPauseClicked += PauseRecording;
            _ui.OnResumeClicked += ResumeRecording;
            _ui.OnStopClicked += StopRecording;

            Thread winMonitor = new Thread(WindowMonitorLoop) { IsBackground = true };
            winMonitor.Start();

            _mouseHookId = SetHook(_mouseProc, WH_MOUSE_LL);
            _kbHookId = SetHook(_kbProc, WH_KEYBOARD_LL);

            Application.Run(_ui);

            UnhookWindowsHookEx(_mouseHookId);
            UnhookWindowsHookEx(_kbHookId);
        }

        private static void StartRecording()
        {
            string root = Path.GetDirectoryName(Path.GetDirectoryName(Application.ExecutablePath));
            string recRoot = Path.Combine(root, "recordings");
            if (!Directory.Exists(recRoot)) Directory.CreateDirectory(recRoot);

            string sessName = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _sessionFolder = Path.Combine(recRoot, sessName);
            Directory.CreateDirectory(_sessionFolder);
            _jsonPath = Path.Combine(_sessionFolder, "events.json");
            _audioPath = Path.Combine(_sessionFolder, "audio.wav");

            _events.Clear();
            _cellBuffer.Length = 0;
            _activeCellName = "";
            _activeElementInfo = null;
            _inEditSession = false;
            _startTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

            bool audioStarted = false;
            if (_ui.IsMicEnabled)
            {
                audioStarted = _audioEngine.Start(_ui.SelectedMicDeviceId, _audioPath);
            }

            var details = new Dictionary<string, object>();
            details["sessionFolder"] = _sessionFolder;
            details["audioRecording"] = audioStarted;
            if (audioStarted)
            {
                details["audioFile"] = "audio.wav";
                details["audioFormat"] = "PCM 16-bit";
            }

            AddEvent("session_start", "Registrazione avviata dalla barra fluttuante", details);
        }

        private static void PauseRecording()
        {
            CommitCellSession("Pausa");
            if (_ui.IsMicEnabled) _audioEngine.Pause();
        }

        private static void ResumeRecording()
        {
            if (_ui.IsMicEnabled) _audioEngine.Resume();
        }

        private static void StopRecording()
        {
            CommitCellSession("Chiusura sessione");
            if (_ui.IsMicEnabled)
            {
                _audioEngine.Stop();
            }
            SaveOutputs();
            ThreadPool.QueueUserWorkItem(new WaitCallback(DelayedExit));
        }

        private static void DelayedExit(object state)
        {
            Thread.Sleep(900);
            Application.Exit();
        }

        private static void CommitCellSession(string confirmKey)
        {
            if (_inEditSession && _cellBuffer.Length > 0)
            {
                string finalVal = _cellBuffer.ToString();
                string targetCell = !string.IsNullOrEmpty(_activeCellName) ? _activeCellName : "Cella attiva";
                
                var details = new Dictionary<string, object>();
                details["cell"] = targetCell;
                details["value"] = finalVal;
                details["confirmedWith"] = confirmKey;
                details["processName"] = _activeApp;
                details["element"] = _activeElementInfo;

                string desc = "Cella " + targetCell + " ➔ Inserito valore: \"" + finalVal + "\" (" + confirmKey + ")";
                AddEvent("cell_commit", desc, details);
            }
            _cellBuffer.Length = 0;
            _inEditSession = false;
        }

        private static void AddEvent(string type, string description, Dictionary<string, object> details)
        {
            lock (_events)
            {
                long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                long elapsed = _startTime > 0 ? (now - _startTime) : 0;

                var ev = new Dictionary<string, object>();
                ev["index"] = _events.Count + 1;
                ev["type"] = type;
                ev["timestamp"] = now;
                ev["elapsedMs"] = elapsed;
                ev["description"] = description;
                ev["details"] = details;

                _events.Add(ev);
                _ui.SetEventCount(_events.Count);
            }
        }

        private static void SaveOutputs()
        {
            try
            {
                if (string.IsNullOrEmpty(_jsonPath) || _events.Count == 0) return;
                string json = SerializeJson(_events);
                File.WriteAllText(_jsonPath, json, Encoding.UTF8);
            }
            catch { }
        }

        private static IntPtr SetHook(Delegate proc, int hookType)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(hookType, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _ui != null && _ui.IsRecording && !_ui.IsPaused)
            {
                int msg = wParam.ToInt32();
                string clickType = null;
                string button = "left";

                if (msg == WM_LBUTTONDOWN) { clickType = "click"; button = "left"; }
                else if (msg == WM_RBUTTONDOWN) { clickType = "click"; button = "right"; }
                else if (msg == WM_MBUTTONDOWN) { clickType = "click"; button = "middle"; }
                else if (msg == WM_LBUTTONDBLCLK) { clickType = "dblclick"; button = "left"; }

                if (clickType != null)
                {
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    Point pt = new Point(hookStruct.pt.x, hookStruct.pt.y);

                    if (!_ui.Bounds.Contains(pt))
                    {
                        CommitCellSession("Click esterno");
                        ThreadPool.QueueUserWorkItem(new WaitCallback(delegate {
                            InspectAndRecordMouse(clickType, button, pt);
                        }));
                    }
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _ui != null && _ui.IsRecording && !_ui.IsPaused)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys key = (Keys)vkCode;

                    bool ctrl = (GetKeyState(0x11) & 0x8000) != 0;
                    bool shift = (GetKeyState(0x10) & 0x8000) != 0;
                    bool alt = (GetKeyState(0x12) & 0x8000) != 0;
                    bool win = ((GetKeyState(0x5B) & 0x8000) != 0) || ((GetKeyState(0x5C) & 0x8000) != 0);

                    if (key == Keys.Return)
                    {
                        CommitCellSession("Invio");
                        return CallNextHookEx(_kbHookId, nCode, wParam, lParam);
                    }
                    if (key == Keys.Tab)
                    {
                        CommitCellSession("Tab");
                        return CallNextHookEx(_kbHookId, nCode, wParam, lParam);
                    }

                    if (vkCode == 8)
                    {
                        if (_cellBuffer.Length > 0)
                        {
                            _cellBuffer.Length--;
                        }
                        return CallNextHookEx(_kbHookId, nCode, wParam, lParam);
                    }

                    if (!ctrl && !alt && !win)
                    {
                        byte[] keyState = new byte[256];
                        GetKeyboardState(keyState);
                        StringBuilder sbChar = new StringBuilder(4);
                        int res = ToUnicode((uint)vkCode, 0, keyState, sbChar, sbChar.Capacity, 0);

                        if (res > 0)
                        {
                            _inEditSession = true;
                            _cellBuffer.Append(sbChar.ToString());
                            return CallNextHookEx(_kbHookId, nCode, wParam, lParam);
                        }
                    }

                    if (ctrl || alt)
                    {
                        CommitCellSession("Scorciatoia");
                        if (vkCode != 160 && vkCode != 161 && vkCode != 162 && vkCode != 163 && vkCode != 164 && vkCode != 165)
                        {
                            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate {
                                InspectAndRecordKey(key, vkCode, ctrl, shift, alt, win);
                            }));
                        }
                    }
                }
            }
            return CallNextHookEx(_kbHookId, nCode, wParam, lParam);
        }

        private static void WindowMonitorLoop()
        {
            while (true)
            {
                try
                {
                    if (_ui != null && _ui.IsRecording && !_ui.IsPaused)
                    {
                        IntPtr hwnd = GetForegroundWindow();
                        if (hwnd != IntPtr.Zero && hwnd != _lastForegroundWindow && hwnd != _ui.Handle)
                        {
                            _lastForegroundWindow = hwnd;
                            string title = GetWindowTextRaw(hwnd);
                            string procName = GetProcessNameFromHwnd(hwnd);

                            if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(procName))
                            {
                                _lastWindowTitle = title;
                                _lastProcessName = procName;
                                _activeApp = procName;

                                CommitCellSession("Cambio finestra");

                                var details = new Dictionary<string, object>();
                                details["windowTitle"] = title;
                                details["processName"] = procName;
                                details["hwnd"] = hwnd.ToInt64();
                                AddEvent("window_switch", "Finestra attiva: '" + title + "' (App: " + procName + ")", details);
                            }
                        }
                    }
                }
                catch { }
                Thread.Sleep(250);
            }
        }

        private static void InspectAndRecordMouse(string clickType, string button, Point pt)
        {
            try
            {
                AutomationElement el = AutomationElement.FromPoint(new System.Windows.Point(pt.X, pt.Y));
                Dictionary<string, object> elementInfo = null;

                if (el != null && el.Current.ProcessId != _currentPid)
                {
                    elementInfo = ExtractElementInfo(el);
                    _activeElementInfo = elementInfo;

                    if (elementInfo != null && elementInfo.ContainsKey("name"))
                    {
                        _activeCellName = elementInfo["name"].ToString();
                        _activeApp = elementInfo.ContainsKey("processName") ? elementInfo["processName"].ToString() : "";
                    }
                }

                string btnName = button == "right" ? "destro" : (button == "middle" ? "centrale" : "sinistro");
                string actName = clickType == "dblclick" ? "Doppio click" : "Click";
                string desc = actName + " con tasto " + btnName;

                if (elementInfo != null && elementInfo.ContainsKey("controlType"))
                {
                    string elName = elementInfo.ContainsKey("name") && !string.IsNullOrEmpty((string)elementInfo["name"]) ? "'" + elementInfo["name"] + "'" : "senza nome";
                    desc += " su " + elementInfo["controlType"] + " " + elName + " in '" + elementInfo["processName"] + "'";
                }
                else
                {
                    desc += " alle coordinate (X: " + pt.X + ", Y: " + pt.Y + ")";
                }

                var details = new Dictionary<string, object>();
                details["action"] = clickType;
                details["button"] = button;
                details["x"] = pt.X;
                details["y"] = pt.Y;
                details["element"] = elementInfo;

                AddEvent("mouse_action", desc, details);
            }
            catch
            {
                var details = new Dictionary<string, object>();
                details["action"] = clickType;
                details["button"] = button;
                details["x"] = pt.X;
                details["y"] = pt.Y;
                AddEvent("mouse_action", "Click alle coordinate (X: " + pt.X + ", Y: " + pt.Y + ")", details);
            }
        }

        private static void InspectAndRecordKey(Keys key, int vkCode, bool ctrl, bool shift, bool alt, bool win)
        {
            try
            {
                AutomationElement el = AutomationElement.FocusedElement;
                Dictionary<string, object> elementInfo = null;
                if (el != null && el.Current.ProcessId != _currentPid) elementInfo = ExtractElementInfo(el);

                List<string> mods = new List<string>();
                if (ctrl) mods.Add("Ctrl");
                if (shift) mods.Add("Shift");
                if (alt) mods.Add("Alt");
                if (win) mods.Add("Win");
                mods.Add(key.ToString());
                string combo = string.Join("+", mods.ToArray());

                string target = elementInfo != null && elementInfo.ContainsKey("controlType") ? " nel controllo " + elementInfo["controlType"] + " '" + elementInfo["name"] + "'" : "";

                var details = new Dictionary<string, object>();
                details["key"] = key.ToString();
                details["vkCode"] = vkCode;
                details["ctrl"] = ctrl;
                details["shift"] = shift;
                details["alt"] = alt;
                details["win"] = win;
                details["element"] = elementInfo;

                AddEvent("key_action", "Premuto tasto [" + combo + "]" + target, details);
            }
            catch { }
        }

        private static Dictionary<string, object> ExtractElementInfo(AutomationElement el)
        {
            if (el == null) return null;
            try
            {
                var current = el.Current;
                var info = new Dictionary<string, object>();

                info["name"] = current.Name ?? "";
                info["controlType"] = current.ControlType != null ? current.ControlType.ProgrammaticName.Replace("ControlType.", "") : "Unknown";
                info["automationId"] = current.AutomationId ?? "";
                info["className"] = current.ClassName ?? "";
                info["frameworkId"] = current.FrameworkId ?? "";
                info["isPassword"] = current.IsPassword;
                info["isEnabled"] = current.IsEnabled;

                object valPattern;
                if (el.TryGetCurrentPattern(ValuePattern.Pattern, out valPattern))
                {
                    string v = ((ValuePattern)valPattern).Current.Value;
                    info["value"] = current.IsPassword ? "[REDACTED]" : v;
                }

                var rect = current.BoundingRectangle;
                if (rect != System.Windows.Rect.Empty)
                {
                    var bounds = new Dictionary<string, object>();
                    bounds["x"] = (int)rect.X;
                    bounds["y"] = (int)rect.Y;
                    bounds["w"] = (int)rect.Width;
                    bounds["h"] = (int)rect.Height;
                    info["bounds"] = bounds;
                }

                int pid = current.ProcessId;
                info["processId"] = pid;
                try
                {
                    Process p = Process.GetProcessById(pid);
                    info["processName"] = p.ProcessName;
                }
                catch
                {
                    info["processName"] = _lastProcessName;
                }

                List<string> pathList = new List<string>();
                try
                {
                    AutomationElement parent = TreeWalker.ControlViewWalker.GetParent(el);
                    int hops = 0;
                    while (parent != null && hops < 5)
                    {
                        string pName = parent.Current.Name;
                        string pType = parent.Current.ControlType != null ? parent.Current.ControlType.ProgrammaticName.Replace("ControlType.", "") : "";
                        if (!string.IsNullOrEmpty(pName))
                        {
                            pathList.Insert(0, pName + " (" + pType + ")");
                        }
                        if (parent == AutomationElement.RootElement) break;
                        parent = TreeWalker.ControlViewWalker.GetParent(parent);
                        hops++;
                    }
                }
                catch { }
                info["hierarchy"] = pathList;

                return info;
            }
            catch
            {
                return null;
            }
        }

        private static string SerializeJson(object obj)
        {
            if (obj == null) return "null";
            if (obj is string) return "\"" + EscapeString((string)obj) + "\"";
            if (obj is bool) return ((bool)obj) ? "true" : "false";
            if (obj is int || obj is long) return obj.ToString();
            if (obj is double || obj is float) return Convert.ToString(obj, CultureInfo.InvariantCulture);

            IDictionary<string, object> dict = obj as IDictionary<string, object>;
            if (dict != null)
            {
                StringBuilder sb = new StringBuilder("{");
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(",");
                    sb.Append("\"" + EscapeString(kv.Key) + "\":");
                    sb.Append(SerializeJson(kv.Value));
                    first = false;
                }
                sb.Append("}");
                return sb.ToString();
            }

            System.Collections.IEnumerable list = obj as System.Collections.IEnumerable;
            if (list != null)
            {
                StringBuilder sb = new StringBuilder("[");
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(",");
                    sb.Append(SerializeJson(item));
                    first = false;
                }
                sb.Append("]");
                return sb.ToString();
            }

            return "\"" + EscapeString(obj.ToString()) + "\"";
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }

        private static string GetWindowTextRaw(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString().Trim();
        }

        private static string GetProcessNameFromHwnd(IntPtr hwnd)
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch { return ""; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }
}