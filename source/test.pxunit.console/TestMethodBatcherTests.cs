using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using pxunit.console;

namespace test.pxunit.console
{
    public class TestMethodBatcherTests
    {
        private readonly TestMethodBatcher _batcher;

        public TestMethodBatcherTests()
        {
            _batcher = new TestMethodBatcher(14);
        }

        [Fact]
        public void First_Batch_Start_At_0_With_Size_10()
        {
            var batch = _batcher.GetNextBatch();

            Assert.Equal(0, batch.Start);
            Assert.Equal(10, batch.End);
            Assert.Equal(10, batch.Size);
        }

        [Fact]
        public void Second_Batch_Starts_At_10_With_Size_4()
        {
            _batcher.GetNextBatch();

            var batch = _batcher.GetNextBatch();

            Assert.Equal(10, batch.Start);
            Assert.Equal(14, batch.End);
            Assert.Equal(4, batch.Size);
        }

        [Fact]
        public void Thrid_Batch_Is_Empty()
        {
            _batcher.GetNextBatch();
            _batcher.GetNextBatch();

            var batch = _batcher.GetNextBatch();

            Assert.Equal(0, batch.Size);
        }
    }
}
