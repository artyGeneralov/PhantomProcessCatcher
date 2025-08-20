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
        private readonly ConcurrentDictionary<int, HashSet<DllData>> _liveDlls = new ConcurrentDictionary<int, HashSet<DllData>>(); // Tracks libraries of currently live processes (since start of monitoring)
        private readonly ConcurrentDictionary<string, HashSet<DllData>> _shortLivedDlls = new ConcurrentDictionary<string, HashSet<DllData>>(); // Saves libraries of short-lived processes that have already stopped.
        private readonly ConcurrentDictionary<int, HashSet<HandleData>> _liveHandles = new ConcurrentDictionary<int, HashSet<HandleData>>(); // Tracks the handles for the live process list.
        private readonly ConcurrentDictionary<string, HashSet<HandleData>> _shortLivedHandles = new ConcurrentDictionary<string, HashSet<HandleData>>(); // Saves the handles for the short lived process (since start of monitoring)


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
            _session = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            _session.StopOnDispose = false;

            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process 
                | KernelTraceEventParser.Keywords.ImageLoad 
                | KernelTraceEventParser.Keywords.Handle
                );

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
            int process_id = data.ProcessID;
            if (!_liveProcesses.ContainsKey(process_id)) return;
            if (!_liveHandles.ContainsKey(process_id))
            {
                _liveHandles[process_id] = new HashSet<HandleData>();
            }
            _liveHandles[process_id].Add(new HandleData(data.ProcessID, data.Object, data.ObjectTypeName, data.ObjectName));
        }

        private void processStopped(ProcessTraceData data)
        {
            if (!_liveProcesses.TryGetValue(data.ProcessID, out ProcessEntry entry)) return;
            double elapsed = (data.TimeStamp.ToUniversalTime() - entry.CreationTime).TotalSeconds;
            if(elapsed < Interval)
            {
                string processIdString = _getShortProcessString(data.ProcessID, entry.CreationTime);
                if (!_shortLivedProcesses.ContainsKey(processIdString))
                {
                    // add process
                    _shortLivedProcesses[processIdString] = new ProcessEntry(entry);

                    // add dlls
                    if (_liveDlls.ContainsKey(data.ProcessID))
                    {
                        _shortLivedDlls[processIdString] = new HashSet<DllData>(_liveDlls[data.ProcessID]);
                    }

                    // add handles
                    if(_liveHandles.ContainsKey(data.ProcessID))
                    {
                        _shortLivedHandles[processIdString] = new HashSet<HandleData>(_liveHandles[data.ProcessID]);
                    }


                    // remove
                    _liveDlls.TryRemove(data.ProcessID, out _);
                    _liveProcesses.TryRemove(data.ProcessID, out _);
                    _liveHandles.TryRemove(data.ProcessID, out _);
                    // notify
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
                _liveDlls[data.ProcessID] = new HashSet<DllData>();
            }

            _liveDlls[data.ProcessID].Add(new DllData(data.FileName, data.FileName));
        }

        public IReadOnlyList<DllData> GetDllsForProcess(ProcessEntry entry)
        {
            string key = _getShortProcessString(entry.Pid, entry.CreationTime);

            return _shortLivedDlls.TryGetValue(key, out var dlls) ? new List<DllData>(dlls) : new List<DllData>(0);
        }

        public IReadOnlyList<HandleData> GetHandlesForProcess(ProcessEntry entry)
        {
            string key = _getShortProcessString(entry.Pid, entry.CreationTime);
            return _shortLivedHandles.TryGetValue(key, out var handles) ? new List<HandleData>(handles) : new List<HandleData>(0);
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
