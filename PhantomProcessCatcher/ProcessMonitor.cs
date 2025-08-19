using NtApiDotNet;
using PhantomProcessCatcher.data;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ConcurrentDictionary<int, ProcessEntry> _live = new ConcurrentDictionary<int, ProcessEntry>();
        private readonly ConcurrentDictionary<int, ProcessEntry> _shortLivedProcesses = new ConcurrentDictionary<int, ProcessEntry>();
        private readonly ConcurrentDictionary<int, HashSet<string>> _dlls = new ConcurrentDictionary<int, HashSet<string>>();

        private int procNum = 1;

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

            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.ImageLoad);

            _session.Source.Kernel.ProcessStart += processStarted;
            _session.Source.Kernel.ProcessStop += processStopped;
            _session.Source.Kernel.ImageLoad += dllLoaded;

            _pumpTask = Task.Run(() => _session.Source.Process());
        }

        private void processStarted(ProcessTraceData data)
        {
            if (!_live.ContainsKey(data.ProcessID))
            {
                ProcessEntry e = new ProcessEntry(data.ProcessID, data.ProcessName, DateTime.UtcNow, "User");
                _live[data.ProcessID] = e;
            }
        }

        private void processStopped(ProcessTraceData data)
        {
            if (!_live.ContainsKey(data.ProcessID)) return;
            TimeSpan elapsed = DateTime.UtcNow - _live[data.ProcessID].CreationTime;
            if(elapsed.Seconds < Interval)
            {
                if (!_shortLivedProcesses.ContainsKey(data.ProcessID))
                {
                    ProcessEntry e = _live[data.ProcessID];
                    _shortLivedProcesses[data.ProcessID] = new ProcessEntry(e);
                    _live.TryRemove(data.ProcessID, out _);
                    ShortLivedDetected?.Invoke(this, e);
                }
            }
            else
            {
                _live.TryRemove(data.ProcessID, out _);
            }
        }

        private void dllLoaded(ImageLoadTraceData data)
        {
            if (!_live.ContainsKey(data.ProcessID)) return;
            if (!_dlls.ContainsKey(data.ProcessID))
            {
                _dlls[data.ProcessID] = new HashSet<string>();
            }
            _dlls[data.ProcessID].Add(data.FileName);
            Console.WriteLine($"Dll {data.FileName}.dll has been loaded");
        }

        public HashSet<string> GetDllsForProcess(int pid)
        {
            return _dlls.TryGetValue(pid, out var dlls) ? dlls : new HashSet<string>();
        }
        public void StopMonitoring() 
        {
            if (!IsRunning) return;
            _session?.Dispose();
            _pumpTask?.Wait(2000);
            _pumpTask = null;
            IsRunning = false;

        }





        public List<ProcessEntry> GetAllProcesses()
        {
            List<ProcessEntry> processList = new List<ProcessEntry>();

            Process[] allActiveProcesses = Process.GetProcesses();
            int Pid = 0;
            string Name = "Default Process Name";
            DateTime CreationTime = DateTime.UtcNow;
            string User = "Default User";
            
            foreach(Process p in allActiveProcesses)
            {
                try { Pid = p.Id; } catch { Console.WriteLine($"Couldnt get pid for process {p.ProcessName}"); }
                try { Name = p.ProcessName; } catch { }
                try { CreationTime = p.StartTime; } catch { Console.WriteLine($"Couldnt get startTime for process {p.ProcessName}"); }


                ProcessEntry entry = new ProcessEntry();
                processList.Add(entry);
            }

            return processList;
        }


        
    }
}
