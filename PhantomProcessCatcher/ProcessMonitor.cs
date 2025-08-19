using PhantomProcessCatcher.data;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.Collections.Concurrent;
using Microsoft.Diagnostics.Tracing.Parsers;


namespace PhantomProcessCatcher
{
    public class ProcessMonitor
    {
        public int Interval { get; set; }
        public bool IsRunning { get; private set; }
        public event EventHandler<ProcessEntry> ShortLivedDetected;

        private TraceEventSession _session;
        private Task _pumpTask;

        private readonly ConcurrentDictionary<int, ProcessEntry> _liveProcesses = new ConcurrentDictionary<int, ProcessEntry>(); // Tracks the live processes that are currently running (since start of monitoring)
        private readonly ConcurrentDictionary<string, ProcessEntry> _shortLivedProcesses = new ConcurrentDictionary<string, ProcessEntry>(); // Saves short lived processes that have already stopped.
        private readonly ConcurrentDictionary<int, HashSet<string>> _liveDlls = new ConcurrentDictionary<int, HashSet<string>>(); // Tracks libraries of currently live processes (since start of monitoring)
        private readonly ConcurrentDictionary<string, HashSet<string>> _shortLivedDlls = new ConcurrentDictionary<string, HashSet<string>>(); // Saves libraries of short-lived processes that have already stopped.


        public ProcessMonitor()
        {
            Interval = 10;
        }


        public void StartMonitoring()
        {
            if (IsRunning) return;
            if (TraceEventSession.IsElevated() != true)
            {
                throw new InvalidOperationException("Run as Administrator.");
            }

            IsRunning = true;
            _session = new TraceEventSession($"PhantomProcessCatcher-{Guid.NewGuid()}");
            _session.StopOnDispose = true;

            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.ImageLoad | KernelTraceEventParser.Keywords.Handle);

            _session.Source.Kernel.ProcessStart += processStarted;
            _session.Source.Kernel.ProcessStop += processStopped;
            _session.Source.Kernel.ImageLoad += dllLoaded;
            _session.Source.Kernel.ObjectCreateHandle += HandleCreated;

            _pumpTask = Task.Run(() => _session.Source.Process());
        }

        private void processStarted(ProcessTraceData data)
        {
            if (!_liveProcesses.ContainsKey(data.ProcessID))
            {
                ProcessEntry e = new ProcessEntry(data.ProcessID, data.ProcessName, data.TimeStamp.ToUniversalTime(), "User");
                _liveProcesses[data.ProcessID] = e;
            }
        }

        private void HandleCreated(ObjectHandleTraceData data)
        {

        }

        private void processStopped(ProcessTraceData data)
        {
            if (!_liveProcesses.TryGetValue(data.ProcessID, out ProcessEntry entry)) return;
            double elapsed = (data.TimeStamp.ToUniversalTime() - entry.CreationTime).TotalSeconds;
            if(elapsed < Interval)
            {
                string processId = _getShortProcessString(data.ProcessID, entry.CreationTime);
                if (!_shortLivedProcesses.ContainsKey(processId))
                {
                    _shortLivedProcesses[processId] = new ProcessEntry(entry);
                    if (_liveDlls.ContainsKey(data.ProcessID))
                    {
                        _shortLivedDlls[processId] = new HashSet<string>(_liveDlls[data.ProcessID]);
                    }
                    _liveDlls.TryRemove(data.ProcessID, out _);
                    _liveProcesses.TryRemove(data.ProcessID, out _);
                    ShortLivedDetected?.Invoke(this, entry);
                }
            }
            else
            {
                _liveProcesses.TryRemove(data.ProcessID, out _);
                _liveDlls.TryRemove(data.ProcessID, out _);
            }
        }
        private string _getShortProcessString(int pid, DateTime creationTime)
        {
            return pid + creationTime.Ticks.ToString();
        }

        private void dllLoaded(ImageLoadTraceData data)
        {
            if (!_liveProcesses.ContainsKey(data.ProcessID)) return;
            if (!_liveDlls.ContainsKey(data.ProcessID))
            {
                _liveDlls[data.ProcessID] = new HashSet<string>();
            }
            _liveDlls[data.ProcessID].Add(data.FileName);
        }

        public IReadOnlyList<string> GetDllsForProcess(ProcessEntry entry)
        {
            string key = _getShortProcessString(entry.Pid, entry.CreationTime);

            return _shortLivedDlls.TryGetValue(key, out var dlls) ? new List<string>(dlls) : new List<string>(0);
        }
        public void StopMonitoring() 
        {
            if (!IsRunning) return;
            _session?.Dispose();
            _pumpTask?.Wait(2000);
            _pumpTask = null;
            IsRunning = false;

        }
        
    }
}
