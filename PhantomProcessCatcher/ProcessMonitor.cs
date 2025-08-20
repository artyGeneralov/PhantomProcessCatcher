using PhantomProcessCatcher.data;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing;
using System.Collections.Concurrent;
using Microsoft.Diagnostics.Tracing.Parsers;
using System.Runtime.InteropServices;


namespace PhantomProcessCatcher
{
    public class ProcessMonitor
    {
        public int Interval { get; set; }
        public bool IsRunning { get; private set; }
        public event EventHandler<ProcessEntry> ShortLivedDetected;

        private ETWTraceEventSource _source;
        private Task _pumpTask;
        private DateTime _acceptEventsAfterUtc;

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


        /* Main etw trace setup and disposal */

        public void StartMonitoring()
        {
            if (IsRunning) return;
            if (TraceEventSession.IsElevated() != true)
            {
                throw new InvalidOperationException("Run as Administrator.");
            }
            _acceptEventsAfterUtc = DateTime.UtcNow;

            /* all of this is here so that the freaking thing restarts properly. 
             * Sometimes after you start the monitoring, stop it, close the program and restart - it would give you this 0x80071069 error, and tell you that the instance name passed
             * is not recognized as a WMI data provider. The devil knows what causes that error, but restarting everything seems to get it working...
             */
            void StartPump()
            {
                _source = new ETWTraceEventSource(KernelTraceEventParser.KernelSessionName,
                    TraceEventSourceType.Session);
                KernelTraceEventParser kernel = new KernelTraceEventParser(_source);

                kernel.ProcessStart += processStarted;
                kernel.ProcessStop += processStopped;
                kernel.ImageLoad += dllLoaded;
                kernel.ObjectCreateHandle += HandleCreated;

                _pumpTask = Task.Run(() =>
                {

                    try
                    {
                        _source.Process();
                    }
                    catch (COMException ex) when ((uint)ex.HResult == 0x80071069)
                    {
                        try
                        {
                            _source.Dispose();
                        }
                        catch { }

                        EnsureKernelKeywords(); // (re)start the kernel logger
                        _source = new ETWTraceEventSource(KernelTraceEventParser.KernelSessionName,
                                                          TraceEventSourceType.Session);
                        KernelTraceEventParser k2 = new KernelTraceEventParser(_source);
                        k2.ProcessStart += processStarted;
                        k2.ProcessStop += processStopped;
                        k2.ImageLoad += dllLoaded;
                        k2.ObjectCreateHandle += HandleCreated;

                        _source.Process();
                    }


                });
            }
            EnsureKernelKeywords();
            StartPump();

            IsRunning = true;
        }

        public async Task StopMonitoringAsync()
        {
            if (!IsRunning) return;
            var src = _source;
            var pump = _pumpTask;
            try { src?.StopProcessing(); } catch { }

            if (pump != null)
                await pump.ConfigureAwait(true);
            try { src?.Dispose(); } catch { }
            _source = null;
            _pumpTask = null;
            IsRunning = false;
        }

        /* Callbacks for the etw thing:
         * - processStarted
         * - processStopped
         * - dllLoaded (ImageLoaded is probably more correct, but I'll go with dll...)
         * - HandleCreated
         */
        private void processStarted(ProcessTraceData data)
        {
            if (_tooOld(data.TimeStamp.ToUniversalTime())) return;
            if (!_liveProcesses.ContainsKey(data.ProcessID))
            {
                ProcessEntry e = new ProcessEntry(data.ProcessID, data.ProcessName, data.TimeStamp.ToUniversalTime(), "User");
                _liveProcesses[data.ProcessID] = e;
            }
        }
        private void processStopped(ProcessTraceData data)
        {
            if (_tooOld(data.TimeStamp.ToUniversalTime())) return;
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
            if (_tooOld(data.TimeStamp.ToUniversalTime())) return;
            int process_id = data.ProcessID;
            if (!_liveProcesses.ContainsKey(process_id)) return;
            if (!_liveHandles.ContainsKey(process_id))
            {
                _liveHandles[process_id] = new HashSet<HandleData>();
            }
            _liveHandles[process_id].Add(new HandleData(data.ProcessID, data.Object, data.ObjectTypeName, data.ObjectName));
        }

        private void dllLoaded(ImageLoadTraceData data)
        {
            if (_tooOld(data.TimeStamp.ToUniversalTime())) return;
            if (!_liveProcesses.ContainsKey(data.ProcessID)) return;
            if (!_liveDlls.ContainsKey(data.ProcessID))
            {
                _liveDlls[data.ProcessID] = new HashSet<DllData>();
            }

            string fullPath = data.FileName;
            string name = fullPath?.Substring(fullPath.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            _liveDlls[data.ProcessID].Add(new DllData(name, fullPath));
        }


        /* Data access */
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





        /* Private helpers */

        private void EnsureKernelKeywords()
        {


            var keywords = KernelTraceEventParser.Keywords.Process
                | KernelTraceEventParser.Keywords.ImageLoad
                | KernelTraceEventParser.Keywords.Handle;

            using (var controller = new TraceEventSession(KernelTraceEventParser.KernelSessionName))
            {
                controller.StopOnDispose = false;
                try
                {
                    controller.EnableKernelProvider(keywords);
                }
                catch (COMException e) when ((uint)e.HResult == 0x80070522) { }
            }

        }

        private bool _tooOld(DateTime utc) => utc < _acceptEventsAfterUtc;

        private string _getShortProcessString(int pid, DateTime t) => pid + ":" + t.Ticks;

    }
}
