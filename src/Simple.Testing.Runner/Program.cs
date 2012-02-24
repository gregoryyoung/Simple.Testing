using System;
using System.Collections.Generic;
using System.Linq;
using Simple.Testing.Framework;

namespace Simple.Testing.Runner
{
    class Program
    {
        static void Main(string[] args)
        {
            bool showHelp = false;
            IEnumerable<string> assemblies = Enumerable.Empty<string>();

            var optionSet = new Options() {
                { "h|help", "show this message and exit", x => showHelp = x != null},
                { "a=|assemblies=", "comma-seperated list of the names of assemblies to test", x => assemblies = x.Split(',') }
            };

            try
            {
                optionSet.Parse(args);
                if (showHelp)
                {
                    ShowHelp(optionSet);
                    return;
                }
                if (!assemblies.Any())
                {
                    throw new InvalidOperationException("No assemblies specified.");
                }
            }
            catch (InvalidOperationException exception)
            {
                Console.Write(string.Format("{0}: ", AppDomain.CurrentDomain.FriendlyName));
                Console.WriteLine(exception.Message);
                Console.WriteLine("Try {0} --help for more information", AppDomain.CurrentDomain.FriendlyName);
                return;
            }
            assemblies.ForEach(x => new PrintFailuresOutputter().Output(x, SimpleRunner.RunAllInAssembly(x)));
        }

        private static void ShowHelp(Options optionSet)
        {
            Console.WriteLine("Test specification runner for Simple.Testing");
            Console.WriteLine();
            Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(Console.Out);
        }
    }

    internal class PrintFailuresOutputter
    {
        public void Output(string assembly, IEnumerable<RunResult> results)
        {
            Console.WriteLine("\nRunning all specifications from {0}\n", assembly);
            Console.WriteLine(new string('-', 80));
            int totalCount = 0;
            int totalAsserts = 0;
            int fail = 0;
            int failAsserts = 0;
            foreach (var result in results)
            {
                PrintSpec(result);
                if (!result.Passed)
                {
                    failAsserts += result.Expectations.Where(x => x.Passed == false).Count();
                    fail++;
                }
                totalAsserts += result.Expectations.Count;
                totalCount++;
            }
            Console.WriteLine("\nRan {0} specifications {1} failures. {2} total assertions {3} failures.", totalCount, fail, totalAsserts, failAsserts);
            Console.WriteLine(new string('*', 80));
        }

        private static void PrintSpec(RunResult result)
        {
            var passed = result.Passed ? "PASSED" : "FAILED";
            Console.WriteLine(result.Name + " - " + passed);
            var on = result.GetOnResult();
            if (on != null)
            {
                Console.WriteLine();
                Console.WriteLine("On:");
                Console.WriteLine("\t" + on.ToString());
                Console.WriteLine();
            }
            if (result.Result != null)
            {
                Console.WriteLine();
                Console.WriteLine("Results with:");
                if (result.Result is Exception)
                    Console.WriteLine("\t" + result.Result.GetType() + "\n\t" + ((Exception)result.Result).Message);
                else
                    Console.WriteLine("\t" + result.Result);
                Console.WriteLine();
            }

            Console.WriteLine("Expectations:");
            foreach (var expecation in result.Expectations)
            {
                if (expecation.Passed)
                    Console.WriteLine("\t" + expecation.Text + " - " + (expecation.Passed ? "PASSED" : "FAILED"));
                else
                    Console.WriteLine("\t" + expecation.Exception.Message);
            }
            if (result.Thrown != null)
            {
                Console.WriteLine("Specification failed: " + result.Message);
                Console.WriteLine();
                Console.WriteLine(result.Thrown);
            }
            Console.WriteLine(new string('-', 80));
        }
    }
}
