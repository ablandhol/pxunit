using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.ConsoleClient;

namespace pxunit.console
{
    class ParallelXunitCommandLine
    {
        public const string StartArgument = "/start";
        public const string EndArgument = "/end";

        private readonly string[] _args;
        public int? Start { get; private set; }
        public int? End { get; private set; }

        public ParallelXunitCommandLine(string[] args)
        {
            _args = args;

            DoParse();
        }

        public static ParallelXunitCommandLine Parse(string[] args)
        {
            return new ParallelXunitCommandLine(args);                        
        }

        public bool HasValues
        {
            get { return Start.HasValue && End.HasValue; }
        }

        public void RemoveArguments(IDictionary<string, string> output)
        {
            RemoveArgument(output, StartArgument.Replace("/", ""));
            RemoveArgument(output, EndArgument.Replace("/", ""));
        }

        private void RemoveArgument(IDictionary<string, string> output, string key)
        {
            if (output.ContainsKey(key))
                output.Remove(key);
        }

        private void DoParse()
        {
            Start = GetInt(StartArgument);
            End = GetInt(EndArgument);
        }

        private int? GetInt(string key)
        {
            var value = GetValue(key);
            if (string.IsNullOrEmpty(value))
                return null;

            return Convert.ToInt32(value);
        }

        private string GetValue(string key)
        {
            var args = _args.ToList();
            var idx = args.IndexOf(key);
            if (idx != -1 && (idx + 1) < args.Count)
                return _args[idx + 1];

            return string.Empty;
        }
    }

    public class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Console.WriteLine("xUnit.net console test runner ({0}-bit .NET {1})", IntPtr.Size * 8, Environment.Version);
            Console.WriteLine("Copyright (C) 2007-11 Microsoft Corporation.");

            if (args.Length == 0 || args[0] == "/?")
            {
                PrintUsage();
                return -1;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            try
            {
                int failCount = 0;
                CommandLine commandLine = CommandLine.Parse(args);
                ParallelXunitCommandLine pxCommandLine = ParallelXunitCommandLine.Parse(args);
                if (pxCommandLine.HasValues)
                {
                    failCount = RunProject(commandLine.Project, commandLine.TeamCity, commandLine.Silent, pxCommandLine);    
                }
                else
                {
                    failCount = RunProject(commandLine.Project, commandLine.TeamCity, commandLine.Silent, args);    
                }
                

                if (commandLine.Wait)
                {
                    Console.WriteLine();
                    Console.Write("Press any key to continue...");
                    Console.ReadKey();
                    Console.WriteLine();
                }

                return failCount;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine();
                Console.WriteLine("error: {0}", ex.Message);
                return -1;
            }
            catch (BadImageFormatException ex)
            {
                Console.WriteLine();
                Console.WriteLine("{0}", ex.Message);
                return -1;
            }
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            if (ex != null)
                Console.WriteLine(ex.ToString());
            else
                Console.WriteLine("Error of unknown type thrown in applicaton domain");

            Environment.Exit(1);
        }

        static void PrintUsage()
        {
            string executableName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

            Console.WriteLine();
            Console.WriteLine("usage: {0} <xunitProjectFile> [options]", executableName);
            Console.WriteLine("usage: {0} <assemblyFile> [configFile] [options]", executableName);
            Console.WriteLine();
            Console.WriteLine("Valid options:");
            Console.WriteLine("  /silent                : do not output running test count");
            Console.WriteLine("  /teamcity              : forces TeamCity mode (normally auto-detected)");
            Console.WriteLine("  /wait                  : wait for input after completion");
            Console.WriteLine("  /trait \"name=value\"    : only run tests with matching name/value traits");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine("  /-trait \"name=value\"   : do not run tests with matching name/value traits");
            Console.WriteLine("                         : if specified more than once, acts as an AND operation");
            Console.WriteLine();
            Console.WriteLine("Valid options for assemblies only:");
            Console.WriteLine("  /noshadow              : do not shadow copy assemblies");
            Console.WriteLine("  /xml <filename>        : output results to Xunit-style XML file");

            foreach (TransformConfigurationElement transform in TransformFactory.GetInstalledTransforms())
            {
                string commandLine = "/" + transform.CommandLine + " <filename>";
                commandLine = commandLine.PadRight(22).Substring(0, 22);

                Console.WriteLine("  {0} : {1}", commandLine, transform.Description);
            }
        }

        static int RunProject(XunitProject project, bool teamcity, bool silent, string[] args)
        {
            var defaultArgs = args.Aggregate((a, b) => a + " " + b);

            var mate = new MultiAssemblyTestEnvironment();            
            foreach (XunitProjectAssembly assembly in project.Assemblies)
            {
                TestAssembly testAssembly = mate.Load(assembly.AssemblyFilename, assembly.ConfigFilename, assembly.ShadowCopy);
                var methods = new List<TestMethod>(testAssembly.EnumerateTestMethods(project.Filters.Filter));

                var threads = 12; // Environment.ProcessorCount;
                var methodsPerThread = methods.Count / threads;
                var handledTests = threads*methodsPerThread;
                if (methods.Count < threads)
                {
                    threads = methods.Count;
                    methodsPerThread = 1;
                }

                Console.WriteLine("Running with {0} threads.", threads);
                Console.WriteLine("Total number of tests: {0}.", methods.Count);

                var output = new ConcurrentDictionary<int, List<String>>();
                var processes = new List<Process>();
                for (int i = 0; i < threads; i++)
                {
                    var start = i * methodsPerThread;
                    var end = start + methodsPerThread;

                    var process = StartTestProcess(teamcity, silent, defaultArgs, start, end);

                    processes.Add(process);
                }

                if (methods.Count > handledTests)
                {
                    var start = handledTests;
                    var end = start + (methods.Count - start);
                    processes.Add(StartTestProcess(teamcity, silent, defaultArgs, start, end));
                }

                foreach(var process in processes)
                {
                    process.WaitForExit();
                }

                //foreach (var processOutput in output)
                //{
                //    foreach(var line in processOutput.Value)
                //    {
                //        Console.WriteLine("{0}: {1}", processOutput.Key, line);
                //    }
                //}

                mate.Unload(testAssembly);
            }

            return 0;
        }

        private static Process StartTestProcess(bool teamcity, bool silent, string defaultArgs, int start, int end)
        {
            var xunitArgs = string.Format("{0} {1} {2} {3} {4}", defaultArgs, ParallelXunitCommandLine.StartArgument, start, ParallelXunitCommandLine.EndArgument, end);
            if (silent)
                xunitArgs += " /silent";

            if (teamcity)
                xunitArgs += " /teamcity";

            var process = new Process();
            process.StartInfo.FileName = "pxunit.console.exe";
            process.StartInfo.Arguments = xunitArgs;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.OutputDataReceived += (sender, eventArgs) =>
            {
                var p = (sender as Process);
                Console.WriteLine(eventArgs.Data);
                //Console.WriteLine(string.Format("{0}: {1}", p.Id, eventArgs.Data));

                //var lines = output.GetOrAdd(p.Id, new List<string>());
                //lines.Add(eventArgs.Data);
            };

            process.Start();
            process.BeginOutputReadLine();

            return process;
        }

        static int RunProject(XunitProject project, bool teamcity, bool silent, ParallelXunitCommandLine parallelXunitCommandLine)
        {
            if (!parallelXunitCommandLine.Start.HasValue || !parallelXunitCommandLine.End.HasValue)
                return -1;

            int totalAssemblies = 0;
            int totalTests = 0;
            int totalFailures = 0;
            int totalSkips = 0;
            double totalTime = 0;

            var mate = new MultiAssemblyTestEnvironment();

            foreach (XunitProjectAssembly assembly in project.Assemblies)
            {
                parallelXunitCommandLine.RemoveArguments(assembly.Output);

                TestAssembly testAssembly = mate.Load(assembly.AssemblyFilename, assembly.ConfigFilename, assembly.ShadowCopy);
                List<IResultXmlTransform> transforms = TransformFactory.GetAssemblyTransforms(assembly);

                Console.WriteLine();
                Console.WriteLine("xunit.dll:     Version {0}", testAssembly.XunitVersion);
                Console.WriteLine("Test assembly: {0}", testAssembly.AssemblyFilename);
                Console.WriteLine();

                try
                {
                    var start = parallelXunitCommandLine.Start.Value;
                    var end = parallelXunitCommandLine.End.Value;
                    var methods = new List<TestMethod>(testAssembly.EnumerateTestMethods(project.Filters.Filter)).Skip(start).Take(end-start).ToList();
                    if (methods.Count == 0)
                    {
                        Console.WriteLine("Skipping assembly (no tests match the specified filter).");
                        continue;
                    }

                    var callback =
                        teamcity ? (RunnerCallback)new TeamCityRunnerCallback()
                                 : new StandardRunnerCallback(silent, methods.Count);
                    var assemblyXml = testAssembly.Run(methods, callback);

                    ++totalAssemblies;
                    totalTests += callback.TotalTests;
                    totalFailures += callback.TotalFailures;
                    totalSkips += callback.TotalSkips;
                    totalTime += callback.TotalTime;

                    foreach (var transform in transforms)
                        transform.Transform(assemblyXml);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine(ex.Message);
                }

                mate.Unload(testAssembly);
            }

            if (!teamcity && totalAssemblies > 1)
            {
                Console.WriteLine();
                Console.WriteLine("=== {0} total, {1} failed, {2} skipped, took {3} seconds ===",
                                   totalTests, totalFailures, totalSkips, totalTime.ToString("0.000", CultureInfo.InvariantCulture));
            }

            return totalFailures;
        }

        //static int RunProject(XunitProject project, bool teamcity, bool silent)
        //{
        //    int totalAssemblies = 0;
        //    int totalTests = 0;
        //    int totalFailures = 0;
        //    int totalSkips = 0;
        //    double totalTime = 0;

        //    var mate = new MultiAssemblyTestEnvironment();

        //    foreach (XunitProjectAssembly assembly in project.Assemblies)
        //    {
        //        TestAssembly testAssembly = mate.Load(assembly.AssemblyFilename, assembly.ConfigFilename, assembly.ShadowCopy);
        //        List<IResultXmlTransform> transforms = TransformFactory.GetAssemblyTransforms(assembly);

        //        Console.WriteLine();
        //        Console.WriteLine("xunit.dll:     Version {0}", testAssembly.XunitVersion);
        //        Console.WriteLine("Test assembly: {0}", testAssembly.AssemblyFilename);
        //        Console.WriteLine();

        //        try
        //        {
        //            var methods = new List<TestMethod>(testAssembly.EnumerateTestMethods(project.Filters.Filter));
        //            if (methods.Count == 0)
        //            {
        //                Console.WriteLine("Skipping assembly (no tests match the specified filter).");
        //                continue;
        //            }

        //            var threads = 12;// Environment.ProcessorCount;
        //            Console.WriteLine("Running with {0} threads.", threads);
        //            var methodsPerThread = (int)Math.Ceiling((double)methods.Count / (double)threads);

        //            var lockObj = new object();
        //            var assemblyXml = "";
        //            Action<RunnerCallback> addStatistics = (callback) =>
        //            {
        //                lock (lockObj)
        //                {
        //                    totalTests += callback.TotalTests;
        //                    totalFailures += callback.TotalFailures;
        //                    totalSkips += callback.TotalSkips;
        //                    totalTime += callback.TotalTime;
        //                }
        //            };

        //            Action<string> aggregateXml = (xml) =>
        //            {
        //                lock (lockObj)
        //                {
        //                    assemblyXml += xml;
        //                }
        //            };

        //            var tasks = new List<Task>();
        //            for (int i = 0; i < threads; i++)
        //            {
        //                var groupMethods = methods.Skip(i*methodsPerThread).Take(methodsPerThread).ToList();
        //                if (groupMethods.Count > 0)
        //                {
        //                    var task = new Task(() =>
        //                    {
        //                        var callback = teamcity ? (RunnerCallback)new TeamCityRunnerCallback() : new ThreadedRunnerCallback(silent, groupMethods.Count);
        //                        var groupAssemblyXml = testAssembly.Run(groupMethods, callback);

        //                        addStatistics(callback);
        //                        aggregateXml(groupAssemblyXml);
        //                    });

        //                    tasks.Add(task);
        //                    task.Start();
        //                }
        //            }

        //            foreach (var task in tasks)
        //                task.Wait();

        //            ++totalAssemblies;


        //            foreach (var transform in transforms)
        //                transform.Transform(assemblyXml);
        //        }
        //        catch (ArgumentException ex)
        //        {
        //            Console.WriteLine(ex.Message);
        //        }

        //        mate.Unload(testAssembly);
        //    }

        //    if (!teamcity)
        //    {
        //        Console.WriteLine();
        //        Console.WriteLine("=== {0} total, {1} failed, {2} skipped, took {3} seconds ===",
        //                           totalTests, totalFailures, totalSkips, totalTime.ToString("0.000", CultureInfo.InvariantCulture));
        //    }

        //    return totalFailures;
        //}
    }
}
