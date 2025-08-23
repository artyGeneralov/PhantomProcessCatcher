using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using PhantomProcessCatcher.data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace PhantomProcessCatcher
{
    public class ProcessMonitor
    {
        public int Interval { get; set; }
        public bool IsRunning { get; private set; }
        public event EventHandler<ProcessEntry> ShortLivedDetected;

        private ETWTraceEventSource source;
        private Task _pumpTask;
        private DateTime _acceptEventsAfterUtc;


        // TODO: change the HashSet to something thread-safe...
        private readonly ConcurrentDictionary<int, ProcessEntry> _liveProcesses = new ConcurrentDictionary<int, ProcessEntry>(); // Tracks the live processes that are currently running (since start of monitoring)
        private readonly ConcurrentDictionary<string, ProcessEntry> _shortLivedProcesses = new ConcurrentDictionary<string, ProcessEntry>(); // Saves short lived processes that have already stopped.
        //private readonly ConcurrentDictionary<int, HashSet<DllData>> _liveDlls = new ConcurrentDictionary<int, HashSet<DllData>>(); // Tracks libraries of currently live processes (since start of monitoring)
        //private readonly ConcurrentDictionary<string, HashSet<DllData>> _shortLivedDlls = new ConcurrentDictionary<string, HashSet<DllData>>(); // Saves libraries of short-lived processes that have already stopped.
        //private readonly ConcurrentDictionary<int, HashSet<HandleData>> _liveHandles = new ConcurrentDictionary<int, HashSet<HandleData>>(); // Tracks the handles for the live process list.
        //private readonly ConcurrentDictionary<string, HashSet<HandleData>> _shortLivedHandles = new ConcurrentDictionary<string, HashSet<HandleData>>(); // Saves the handles for the short lived process (since start of monitoring)




        // New Concurrent:
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<DllData, byte>> _liveDlls = new ConcurrentDictionary<int, ConcurrentDictionary<DllData, byte>>();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<DllData, byte>> _shortLivedDlls = new ConcurrentDictionary<string, ConcurrentDictionary<DllData, byte>>();
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<HandleData, byte>> _liveHandles = new ConcurrentDictionary<int, ConcurrentDictionary<HandleData, byte>>();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<HandleData, byte>> _shortLivedHandles = new ConcurrentDictionary<string, ConcurrentDictionary<HandleData, byte>>();
        public ProcessMonitor()
        {
            Interval = 10;
        }


        /* Main etw trace setup and disposal */



        public void StartMonitoring()
        {
            if (IsRunning) return;
            if (TraceEventSession.IsElevated() != true)
            {
                throw new InvalidOperationException("Run as Administrator.");
            }
            _acceptEventsAfterUtc = DateTime.UtcNow; // prevents events from piling up in the buffer after monitoring stops and showing up all at once once monitoring is restarted


            /* This is to mitigate the 0x80071069 error that appears when restarting the program after starting monitoring (basically appears every other restart)
               Retrying to reattach the kernel tracer solves the problem */
            EnsureKernelKeywords();
            StartPump();
            IsRunning = true;
        }

        public async Task StopMonitoringAsync()
        {
            if (!IsRunning) return;
            var src = source;
            var pump = _pumpTask;
            try { src?.StopProcessing(); } catch { }

            if (pump != null)
                await pump.ConfigureAwait(true);
            try { src?.Dispose(); } catch { }
            source = null;
            _pumpTask = null;
            IsRunning = false;
        }

        /* Callbacks for the etw thing:
         * - processStarted
         * - processStopped
         * - dllLoaded (ImageLoaded is probably more correct, but I'll go with dll...)
         * - HandleCreated
         */
        private void ProcessStarted(ProcessTraceData data)
        {
            if (TooOld(data.TimeStamp.ToUniversalTime())) return;
            if (!_liveProcesses.ContainsKey(data.ProcessID))
            {
                ProcessEntry e = new ProcessEntry(data.ProcessID, data.ProcessName, data.TimeStamp.ToUniversalTime(), "User");
                _liveProcesses[data.ProcessID] = e;
            }
        }
        private void ProcessStopped(ProcessTraceData data)
        {
            if (TooOld(data.TimeStamp.ToUniversalTime())) return;
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
                        _shortLivedDlls[processIdString] = new ConcurrentDictionary<DllData, byte>(_liveDlls[data.ProcessID]);
                    }

                    // add handles
                    if(_liveHandles.ContainsKey(data.ProcessID))
                    {
                        _shortLivedHandles[processIdString] = new ConcurrentDictionary<HandleData, byte>(_liveHandles[data.ProcessID]);
                        Console.WriteLine($"Added {_shortLivedHandles[processIdString].Count} new handles to process {_shortLivedProcesses[processIdString].Name}");
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
                _liveHandles.TryRemove(data.ProcessID, out _);
            }
        }

        private void HandleCreated(ObjectHandleTraceData data)
        {
            //Console.WriteLine($"Handle for process {data.ProcessName} with the name {data.ObjectName} and type {data.ObjectType}");
            if (TooOld(data.TimeStamp.ToUniversalTime())) return;
            if (data.ObjectName.Equals("")) return;
            int process_id = data.ProcessID;
            if (!_liveProcesses.ContainsKey(process_id)) return;
            if (!_liveHandles.ContainsKey(process_id))
            {
                _liveHandles[process_id] = new ConcurrentDictionary<HandleData, byte>();
            }
            _liveHandles[process_id].TryAdd(new HandleData(data.ProcessID, data.Object, data.ObjectTypeName, data.ObjectName), 0);
        }

        private void dllLoaded(ImageLoadTraceData data)
        {
            if (!InHotWindow(data.ProcessID, data.TimeStamp.ToUniversalTime())) return;
            if (!_liveProcesses.ContainsKey(data.ProcessID)) return;
            
            if (!_liveDlls.ContainsKey(data.ProcessID))
            {
                _liveDlls[data.ProcessID] = new ConcurrentDictionary<DllData, byte>();
            }

            string fullPath = data.FileName;
            string name = fullPath?.Substring(fullPath.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            _liveDlls[data.ProcessID].TryAdd(new DllData(name, fullPath), 0);
        }


        /* Data access */
        public IReadOnlyList<DllData> GetDllsForProcess(ProcessEntry entry)
        {
            string key = _getShortProcessString(entry.Pid, entry.CreationTime);

            return _shortLivedDlls.TryGetValue(key, out var dlls) ? dlls.Keys.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList() : new List<DllData>(0);
        }

        public IReadOnlyList<HandleData> GetHandlesForProcess(ProcessEntry entry)
        {
            string key = _getShortProcessString(entry.Pid, entry.CreationTime);
            return _shortLivedHandles.TryGetValue(key, out var handles) ? new List<HandleData>(handles.Keys) : new List<HandleData>(0);
        }





        /* Private helpers */

        private void AttachKernelHandlers(KernelTraceEventParser p)
        {
            p.ProcessStart += ProcessStarted;
            p.ProcessStop += ProcessStopped;
            p.ImageLoad += dllLoaded;
            //p.ObjectCreateHandle += HandleCreated;
        }

        // I don't know if this needs to be a loop, in my experience two times does the trick. doesn't hurt though I think.
        private void StartPump()
        {
            _pumpTask = Task.Run(() =>
            {
                while (true)
                {
                    EnsureKernelKeywords();
                    source = new ETWTraceEventSource(KernelTraceEventParser.KernelSessionName, TraceEventSourceType.Session);
                    KernelTraceEventParser parser = new KernelTraceEventParser(source);
                    AttachKernelHandlers(parser);

                    try
                    {
                        source.Process();
                        break;
                    }
                    catch (COMException ex) when ((uint)ex.HResult == 0x80071069)
                    {
                        
                        try
                        {
                            source?.Dispose();
                        }
                        catch { }
                        
                        continue;
                    }
                }
            });
        }

        private void EnsureKernelKeywords()
        {
            KernelTraceEventParser.Keywords keywords = KernelTraceEventParser.Keywords.Process
                                                        | KernelTraceEventParser.Keywords.ImageLoad
                                                        | KernelTraceEventParser.Keywords.Handle;

            using (TraceEventSession controller = new TraceEventSession(KernelTraceEventParser.KernelSessionName))
            {
                controller.StopOnDispose = false;
                try
                {
                    controller.EnableKernelProvider(keywords);
                }
                catch (COMException e) when ((uint)e.HResult == 0x80070522) { }
            }
        }

        // better then TooOld(time) method...
        private bool InHotWindow(int pid, DateTime eventUTC)
        {
            ProcessEntry entry;
            if (!_liveProcesses.TryGetValue(pid, out entry)) return false;
            return (eventUTC - entry.CreationTime).TotalSeconds <= Interval;
        }

        private bool TooOld(DateTime utc) => utc < _acceptEventsAfterUtc;

        private string _getShortProcessString(int pid, DateTime t) => pid + ":" + t.Ticks;

    }
}
