using System;
using System.Globalization;
using Xunit;
using Xunit.ConsoleClient;

namespace pxunit.console
{
    public class ThreadedRunnerCallback : StandardRunnerCallback
    {
        public ThreadedRunnerCallback(bool silent, int totalCount) : base(silent, totalCount)
        {
        }
    }
}