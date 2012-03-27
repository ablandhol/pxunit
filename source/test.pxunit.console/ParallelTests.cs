using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace test.pxunit.console
{
    public class ParallelTests
    {
        [Fact]
        public void Add()
        {
            var result = 1 + 1;
            
            Assert.Equal(2, result);
        }

        [Fact]
        public void Remove()
        {
            var result = 2 - 1;

            Assert.Equal(1, result);
        }

        [Fact]
        public void Sleep_5000ms()
        {
            Thread.Sleep(500);

            Assert.True(true);
        }
    }
}
