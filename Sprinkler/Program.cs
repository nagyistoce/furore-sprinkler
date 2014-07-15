﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.github.com/furore-fhir/sprinkler/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sprinkler.Framework;
using Sprinkler.Properties;

namespace Sprinkler
{
    public class Program
    {
        public enum OutputFormat
        {
            Html,
            Xml
        };

        private const string OutputPar = "-o";
        private const string ListPar = "-l";
        private const string FormatPar = "-f";

        public static IDictionary<string, string> KnownPars = new Dictionary<string, string>
        {
            {ListPar, Resources.listPar},
            {FormatPar, Resources.formatPar},
            {OutputPar, Resources.outputPar}
        };

        /**
         * Parameters:
         * -l                                    show options
         * -f                                    output format [o:Html, o:Xml]
         * -o:filepath                           output path+filename 
         * [url]                                 fhir url
         * [codes]                               space-separated list of test codes or test categories
         */

        public static void Main(string[] args)
        {
            var pars = ReadArgs(args);
            var opts = pars.Item1;
            var mandatoryPars = pars.Item2;
            Console.WriteLine(Resources.header);
            Console.WriteLine();
            if (opts.ContainsKey(ListPar))
            {
                ShowModulesList();
            }
            else if (!mandatoryPars.Any() || !TestSets.IsValidUrl(mandatoryPars[0]))
            {
                ShowOptions();
                Console.Error.WriteLine(Resources.missingURL);
            }
            else
            {
                try
                {
                    RunTests(opts, mandatoryPars);
                }
                catch (Exception x)
                {
                    Console.Error.WriteLine(Resources.caughtException, x.Message);
                }
            }
        }

        private static void RunTests(IDictionary<string, string> opts, IList<string> mandatoryPars)
        {
            var url = mandatoryPars[0];
            var results = new TestResults(Resources.header, true);
            var outputFormat = GetOutputFormat(opts);
            var outputFilename = GetOutputFilename(opts, outputFormat);
            var outputWriter = outputFilename == null
                ? Console.Out
                : File.CreateText(GetOutputFilename(opts, outputFormat));

            Console.Write(Resources.testStarted);
            TestSets.Run(url, results, mandatoryPars.Skip(1).ToArray());
            Console.Write("\r{0}\r",new string(' ', Console.WindowWidth - 1));
            ProcessOutputOptions(results, outputWriter, outputFormat);
        }

        private static string GetOutputFilename(IDictionary<string, string> opts, OutputFormat outputFormat)
        {
            if (!opts.ContainsKey(OutputPar)) return null;
            var outputFilename = opts[OutputPar];
            if (String.Empty == outputFilename)
            {
                throw new ArgumentException(string.Format(Resources.wrongValueForParameter, OutputPar, outputFilename));
            }
            var format = outputFormat.ToString().ToLowerInvariant();
            return outputFilename.EndsWith(format, StringComparison.OrdinalIgnoreCase)
                ? outputFilename
                : outputFilename + "." + format;
        }

        private static void ShowModulesList()
        {
            var list = TestSets.GetTestModules();
            Console.WriteLine(Resources.availableModules);
            foreach (var module in list)
            {
                Console.WriteLine(module.Key+":");
                foreach (var method in module.Value)
                {
                    Console.WriteLine("\t{0} {1}", method.Item1, method.Item2);
                }
            }
        }

        private static void ShowOptions()
        {
            var executable = Path.GetFileName(Assembly.GetExecutingAssembly().CodeBase);
            Console.WriteLine("{0} {1}", executable,Resources.syntax);
            Console.WriteLine(Resources.parameters);
            Console.WriteLine(Resources.parametersDesc);
            Console.WriteLine(Resources.options);
            foreach (var opt in KnownPars)
            {
                Console.WriteLine("\t{0}\t{1}", opt.Key, opt.Value);
            }
        }

        private static void ProcessOutputOptions(TestResults results, TextWriter outputWriter, OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Xml)
            {
                results.SerializeTo(outputWriter);
            }
            else
            {
                TextReader xslReader = new StringReader(Resources.xmlToHtml);
                results.SerializeTo(outputWriter, xslReader);
            }
            outputWriter.Close();
        }

        private static OutputFormat GetOutputFormat(IDictionary<string, string> opts)
        {
            var formatOpt = GetOptionValue(opts, FormatPar, OutputFormat.Xml.ToString());
            OutputFormat format;
            if (!Enum.TryParse(formatOpt, true, out format))
            {
                throw new ArgumentException(string.Format(Resources.wrongValueForParameter, FormatPar, formatOpt));
            }
            return format;
        }

        private static string GetOptionValue(IDictionary<string, string> opts, string optionKey,
            string defaultIfNull = null)
        {
            string ret;
            return opts.TryGetValue(optionKey, out ret) ? ret : defaultIfNull;
        }

        private static Tuple<IDictionary<string, string>, string[]> ReadArgs(string[] args)
        {
            IDictionary<string, string> options = new Dictionary<string, string>();
            IList<string> nonOptions = new List<string>();
            if (!args.Any()) return new Tuple<IDictionary<string, string>, string[]>(options, nonOptions.ToArray());
            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    var colon = arg.IndexOf(':');
                    if (colon > -1)
                    {
                        var key = arg.Substring(0, colon);
                        var value = arg.Substring(colon + 1);
                        options.Add(key, value);
                    }
                    else
                    {
                        options.Add(arg, null);
                    }
                }
                else nonOptions.Add(arg);
            }
            return new Tuple<IDictionary<string, string>, string[]>(options, nonOptions.ToArray());
        }
    }
}