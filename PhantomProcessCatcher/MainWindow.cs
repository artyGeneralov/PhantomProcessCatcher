using System;
using System.Windows.Forms;
using System.Collections.Generic;
using PhantomProcessCatcher.data;
using System.ComponentModel;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;


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


        private void gridProcs_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

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
