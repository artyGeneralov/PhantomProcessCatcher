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

        private readonly BindingList<ProcessEntry> _rows = new BindingList<ProcessEntry>();
        
        private readonly ProcessMonitor _monitor = new ProcessMonitor();
        private bool _started = false;
        public MainWindow()
        {
            InitializeComponent();
            gridProcs.AutoGenerateColumns = true;

            gridProcs.DataSource = _rows;

            
        }


        void OnShortLivedProcessDetected(object sender, ProcessEntry e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnShortLivedProcessDetected(sender, e)));
                return;
            }
            Console.WriteLine("Added detected");
            _rows.Add(e);
        }


        private void gridProcs_SelectionChanged(object sender, EventArgs e)
        {
            DataGridViewRow row = gridProcs.CurrentRow;
            ProcessEntry entry = row.DataBoundItem as ProcessEntry;
            var dlls = _monitor.GetDllsForProcess(entry);
            Console.WriteLine($"Showing dlls for process {entry.Name}");
            lstDlls.BeginUpdate();
            try
            {
                lstDlls.DataSource = null;
                lstDlls.DataSource = dlls;
            }
            finally { lstDlls.EndUpdate(); }
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
    }
}
