using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public void SetOutputArguments(IDictionary<string, string> output)
        {
            RemoveArgument(output, StartArgument.Replace("/", ""));
            RemoveArgument(output, EndArgument.Replace("/", ""));

            foreach (var kvp in output.ToList())
            {
                output[kvp.Key] = string.Format("{0}.{1}.{2}.pout", kvp.Value, Start, End);
            }
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
}
