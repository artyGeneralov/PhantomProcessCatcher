using System;
using System.Windows.Forms;
using System.Collections.Generic;
using PhantomProcessCatcher.data;
using System.ComponentModel;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.Linq;


namespace PhantomProcessCatcher
{
    public partial class MainWindow : Form
    {

        private readonly BindingList<ProcessEntry> _processGridRows = new BindingList<ProcessEntry>();
        private readonly BindingList<DllData> _dllDataRows = new BindingList<DllData>();
        private readonly BindingList<HandleData> _handleDataRows = new BindingList<HandleData>();

        private readonly BindingSource _dllSource = new BindingSource();
        private readonly BindingSource _handleSource = new BindingSource();
        private bool _showingDlls = true;
        
        private readonly ProcessMonitor _monitor = new ProcessMonitor();
        private bool _started = false;
        public MainWindow()
        {
            InitializeComponent();
            gridProcs.AutoGenerateColumns = true;

            gridProcs.DataSource = _processGridRows;
            _dllSource.DataSource = _dllDataRows;
            _handleSource.DataSource = _handleDataRows;
            BindDetailsToCurrentMode();

            
        }


        void OnShortLivedProcessDetected(object sender, ProcessEntry e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnShortLivedProcessDetected(sender, e)));
                return;
            }
            Console.WriteLine("Added detected");
            _processGridRows.Add(e);
        }


        private void gridProcs_SelectionChanged(object sender, EventArgs e)
        {
            DataGridViewRow row = gridProcs.CurrentRow;
            ProcessEntry entry = row.DataBoundItem as ProcessEntry;
            

            if (_showingDlls)
            {
                IReadOnlyList<DllData> dlls = _monitor.GetDllsForProcess(entry);
                _dllDataRows.Clear();
                foreach(DllData d in dlls)
                {
                    _dllDataRows.Add(d);
                }
            }
            else
            {
                IReadOnlyList<HandleData> handles = _monitor.GetHandlesForProcess(entry);
                _handleDataRows.Clear();
                foreach(HandleData h in handles)
                {
                    _handleDataRows.Add(h);
                }
            }
           
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_started)
            {
                _monitor.ShortLivedDetected -= OnShortLivedProcessDetected;
                _monitor.StopMonitoring();
                _started = false;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!_started)
            {
                _monitor.ShortLivedDetected += OnShortLivedProcessDetected;
                _started = true;
            }
            _monitor.StartMonitoring();
        }

        private void BindDetailsToCurrentMode()
        {
            gridDetails.SuspendLayout();
            try
            {
                gridDetails.DataSource = null;
                gridDetails.Columns.Clear();
                gridDetails.DataSource = _showingDlls ? _dllSource : _handleSource;
            }
            finally { gridDetails.ResumeLayout(); }
        }

        private void btnToggleDetails_Click(object sender, EventArgs e)
        {
            _showingDlls = !_showingDlls;
            BindDetailsToCurrentMode();
        }
    }
}
