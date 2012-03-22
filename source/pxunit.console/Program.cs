﻿using System;
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
                CommandLine commandLine = CommandLine.Parse(args);

                int failCount = RunProject(commandLine.Project, commandLine.TeamCity, commandLine.Silent);

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

        static int RunProject(XunitProject project, bool teamcity, bool silent)
        {
            int totalAssemblies = 0;
            int totalTests = 0;
            int totalFailures = 0;
            int totalSkips = 0;
            double totalTime = 0;

            var mate = new MultiAssemblyTestEnvironment();

            foreach (XunitProjectAssembly assembly in project.Assemblies)
            {
                TestAssembly testAssembly = mate.Load(assembly.AssemblyFilename, assembly.ConfigFilename, assembly.ShadowCopy);
                List<IResultXmlTransform> transforms = TransformFactory.GetAssemblyTransforms(assembly);

                Console.WriteLine();
                Console.WriteLine("xunit.dll:     Version {0}", testAssembly.XunitVersion);
                Console.WriteLine("Test assembly: {0}", testAssembly.AssemblyFilename);
                Console.WriteLine();

                try
                {
                    var methods = new List<TestMethod>(testAssembly.EnumerateTestMethods(project.Filters.Filter));
                    if (methods.Count == 0)
                    {
                        Console.WriteLine("Skipping assembly (no tests match the specified filter).");
                        continue;
                    }

                    var threads = 12;// Environment.ProcessorCount;
                    Console.WriteLine("Running with {0} threads.", threads);
                    var methodsPerThread = (int)Math.Ceiling((double)methods.Count / (double)threads);
                    //var queuedMethods = 0;

                    //var methodsGroups = new List<List<TestMethod>>();
                    //for(int i = 0; i < threads; i++)
                    //{
                    //    var group = methods.Skip(queuedMethods).Take(methodsPerThread).ToList();
                    //    queuedMethods += group.Count;

                    //    if (group.Count > 0)
                    //        methodsGroups.Add(group);
                    //}


                    var lockObj = new object();
                    var assemblyXml = "";
                    Action<RunnerCallback> addStatistics = (callback) =>
                    {
                        lock (lockObj)
                        {
                            totalTests += callback.TotalTests;
                            totalFailures += callback.TotalFailures;
                            totalSkips += callback.TotalSkips;
                            totalTime += callback.TotalTime;
                        }
                    };

                    Action<string> aggregateXml = (xml) =>
                    {
                        lock (lockObj)
                        {
                            assemblyXml += xml;
                        }
                    };

                    var tasks = new List<Task>();
                    for (int i = 0; i < threads; i++)
                    {
                        var groupMethods = methods.Skip(i*methodsPerThread).Take(methodsPerThread).ToList();
                        if (groupMethods.Count > 0)
                        {
                            var task = new Task(() =>
                            {
                                var callback = teamcity ? (RunnerCallback)new TeamCityRunnerCallback() : new ThreadedRunnerCallback(silent, groupMethods.Count);
                                var groupAssemblyXml = testAssembly.Run(groupMethods, callback);

                                addStatistics(callback);
                                aggregateXml(groupAssemblyXml);
                            });

                            tasks.Add(task);
                            task.Start();
                        }
                    }

                    foreach (var task in tasks)
                        task.Wait();

                    ++totalAssemblies;


                    foreach (var transform in transforms)
                        transform.Transform(assemblyXml);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine(ex.Message);
                }

                mate.Unload(testAssembly);
            }

            if (!teamcity)
            {
                Console.WriteLine();
                Console.WriteLine("=== {0} total, {1} failed, {2} skipped, took {3} seconds ===",
                                   totalTests, totalFailures, totalSkips, totalTime.ToString("0.000", CultureInfo.InvariantCulture));
            }

            return totalFailures;
        }
    }
}
