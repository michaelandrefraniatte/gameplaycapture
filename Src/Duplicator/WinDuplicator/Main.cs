using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace WinDuplicator
{
    public partial class Main : Form
    {
        [DllImport("user32.dll")]
        public static extern bool GetAsyncKeyState(Keys vKey);
        [DllImport("advapi32.dll")]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword, int dwLogonType, int dwLogonProvider, out IntPtr phToken);
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        public static uint CurrentResolution = 0;
        private static Lazy<Icon> _embeddedIcon = new Lazy<Icon>(() => { using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Main), "Duplicator.ico")) return new Icon(stream); });
        public static Icon EmbeddedIcon => _embeddedIcon.Value;

        private WinDuplicatorOptions _options;
        private Duplicator.Duplicator _duplicator;
        private static bool Getstate;
        public static int[] wd = { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        public static int[] wu = { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        public static bool[] ws = { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false };
        static void valchanged(int n, bool val)
        {
            if (val)
            {
                if (wd[n] <= 1)
                {
                    wd[n] = wd[n] + 1;
                }
                wu[n] = 0;
            }
            else
            {
                if (wu[n] <= 1)
                {
                    wu[n] = wu[n] + 1;
                }
                wd[n] = 0;
            }
            ws[n] = val;
        }

        public Main()
        {
            InitializeComponent();
            MinimumSize = Size;
            Icon = EmbeddedIcon;
            _options = new WinDuplicatorOptions();
            propertyGridMain.SelectedObject = _options;

            _duplicator = new Duplicator.Duplicator(_options);
            _duplicator.PropertyChanged += OnDuplicatorPropertyChanged;
            _duplicator.InformationAvailable += OnDuplicatorInformationAvailable;
            _duplicator.Size = new SharpDX.Size2(splitContainerMain.Panel1.Width, splitContainerMain.Panel1.Height);

            splitContainerMain.Panel1.SizeChanged += (sender, e) =>
            {
                _duplicator.Size = new SharpDX.Size2(splitContainerMain.Panel1.Width, splitContainerMain.Panel1.Height);
            };

            splitContainerMain.Panel1.HandleCreated += (sender, e) =>
            {
                _duplicator.Hwnd = splitContainerMain.Panel1.Handle;
            };

            splitContainerMain.Panel1.HandleDestroyed += (sender, e) =>
            {
                _duplicator.Hwnd = IntPtr.Zero;
            };

#if DEBUG
            Text += " - DEBUG";
#endif
            Task.Run(() => Start());
        }
        public void Start()
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            Thread.Sleep(1000);
            checkBoxDuplicate.Checked = true;
            Thread.Sleep(1000);
            checkBoxRecord.Checked = true;
            Thread.Sleep(1000);
            checkBoxRecord.Checked = false;
            for (; ; )
            {
                valchanged(0, GetAsyncKeyState(Keys.NumPad0));
                valchanged(1, GetAsyncKeyState(Keys.Decimal));
                if (wd[1] == 1 & !Getstate)
                {
                    checkBoxRecord.Checked = true;
                    Getstate = true;
                }
                else
                {
                    if (wd[0] == 1 & Getstate)
                    {
                        checkBoxRecord.Checked = false;
                        Getstate = false;
                    }
                }
                Thread.Sleep(70);
            }
        }

        private void OnDuplicatorInformationAvailable(object sender, Duplicator.DuplicatorInformationEventArgs e)
        {
            BeginInvoke((Action)(() =>
            {
                if (!string.IsNullOrEmpty(textBoxStatus.Text))
                {
                    textBoxStatus.AppendText(Environment.NewLine);
                }
                textBoxStatus.AppendText(e.Information);
            }));
        }

        private void OnDuplicatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // update chekboxes accordingly with duplicator state
            // note: these events arrive on another thread
            switch (e.PropertyName)
            {
                case nameof(_duplicator.DuplicatingState):
                    BeginInvoke((Action)(() =>
                    {
                        bool dup = _duplicator.DuplicatingState == Duplicator.DuplicatorState.Started || _duplicator.DuplicatingState == Duplicator.DuplicatorState.Starting;
                        checkBoxDuplicate.Checked = dup;
                        checkBoxRecord.Enabled = dup;
                    }));
                    break;

                case nameof(_duplicator.RecordingState):
                    BeginInvoke((Action)(() =>
                    {
                        checkBoxRecord.Checked = _duplicator.RecordingState == Duplicator.DuplicatorState.Started || _duplicator.RecordingState == Duplicator.DuplicatorState.Starting;
                    }));
                    break;

                case nameof(_duplicator.RecordFilePath):
                    string mode = _duplicator.IsUsingHardwareBasedEncoder ? "Hardware" : "Software";
                    BeginInvoke((Action)(() =>
                    {
                        if (_duplicator.RecordingState == Duplicator.DuplicatorState.Started && !string.IsNullOrEmpty(_duplicator.RecordFilePath))
                        {
                            Text = "Duplicator - " + mode + " Recording " + Path.GetFileName(_duplicator.RecordFilePath);
                        }
                        else
                        {
                            Text = "Duplicator";
                        }
                    }));
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _duplicator?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void buttonQuit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void checkBoxDuplicate_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxDuplicate.Checked)
            {
                _duplicator.StartDuplicating();
            }
            else
            {
                _duplicator.StopDuplicating();
            }
        }

        private void buttonAbout_Click(object sender, EventArgs e)
        {
            var about = new About();
            about.ShowDialog(this);
        }

        private void checkBoxRecord_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxRecord.Checked)
            {
                _duplicator.StartRecording();
            }
            else
            {
                _duplicator.StopRecording();
            }
        }
    }
}
