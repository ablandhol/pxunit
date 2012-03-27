using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace pxunit.console
{
    public class ProcessWaiter
    {
        private readonly ConcurrentDictionary<int, List<string>> _output;
        private readonly ConcurrentDictionary<int, WaitHandle> _waitHandles;

        public int MaximumNumberOfProcesses { get; private set; }
        public ConcurrentDictionary<int, Process> Processes { get; private set; }
        public string DefaultArgs { get; set; }
        public bool TeamCity { get; set; }
        public bool Silent { get; set; }

        public ProcessWaiter(int maximumNumberOfProcesses)
        {
            MaximumNumberOfProcesses = maximumNumberOfProcesses;

            _waitHandles = new ConcurrentDictionary<int, WaitHandle>();
            _output = new ConcurrentDictionary<int, List<string>>();

            Processes = new ConcurrentDictionary<int, Process>();
        }

        public ProcessWaiter() : this(Environment.ProcessorCount)
        {

        }

        public Process StartProcess(TestBatch testBatch)
        {
            WaitForOneProcess();

            var xunitArgs = string.Format("{0} {1} {2} {3} {4}", DefaultArgs, ParallelXunitCommandLine.StartArgument, testBatch.Start, ParallelXunitCommandLine.EndArgument, testBatch.End);
            if (Silent)
                xunitArgs += " /silent";

            if (TeamCity)
                xunitArgs += " /teamcity";

            var process = new Process();
            process.StartInfo.FileName = "pxunit.console.exe";
            process.StartInfo.Arguments = xunitArgs;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.OutputDataReceived += (sender, eventArgs) =>
                                              {
                                                  var p = (Process)sender;
                                                  //Console.WriteLine(eventArgs.Data);
                                                  Console.WriteLine(string.Format("{0}: {1}", p.Id, eventArgs.Data));

                                                  var lines = _output.GetOrAdd(p.Id, new List<string>());
                                                  lines.Add(eventArgs.Data);
                                              };

            process.Start();
            process.BeginOutputReadLine();

            Processes.TryAdd(process.Id, process);
            AddWaitHandle(process);

            return process;
        }

        public void PrintSummary()
        {
            foreach (var waitHandle in WaitHandles)
            {
                waitHandle.WaitOne();
            }

            foreach (var processOutput in _output)
            {
                Console.WriteLine("============ Summary for thread {0} ============", processOutput.Key);
                foreach (var line in processOutput.Value)
                {
                    Console.WriteLine(line);
                }
            }
        }

        private WaitHandle[] WaitHandles
        {
            get { return _waitHandles.Values.ToArray(); }
        }

        private void AddWaitHandle(Process process)
        {
            var waitForProcess = process;
            var autoResetEvent = new AutoResetEvent(false);
            var task = new Task(() => WaitForProcess(waitForProcess, autoResetEvent));

            _waitHandles.TryAdd(process.Id, autoResetEvent);

            task.Start();
        }

        private void WaitForOneProcess()
        {
            if (Processes.Count >= MaximumNumberOfProcesses)
            {
                WaitHandle.WaitAny(WaitHandles);
            }
        }

        private void WaitForProcess(Process process, AutoResetEvent autoResetEvent)
        {
            if (!process.HasExited)
                process.WaitForExit();

            autoResetEvent.Set();

            Process processToRemove;
            Processes.TryRemove(process.Id, out processToRemove);

            WaitHandle waitHandleToRemove;
            _waitHandles.TryRemove(process.Id, out waitHandleToRemove);
        }
    }
}