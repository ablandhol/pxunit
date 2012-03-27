using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace pxunit.console
{
    public class TestMethodBatcher
    {
        private readonly IList<int> _dummyList;
        private readonly int _numberOfTestMethods;
        private readonly int _batchSize;

        public int NextStartIndex { get; private set; }

        public TestMethodBatcher(int numberOfTestMethods, int batchSize = 10)
        {
            _numberOfTestMethods = numberOfTestMethods;
            _batchSize = batchSize;
            _dummyList = Enumerable.Range(1, _numberOfTestMethods).ToList();
            NextStartIndex = 0;
        }

        public TestBatch GetNextBatch()
        {
            var batchSize = _batchSize;
            if (NextStartIndex + _batchSize > _numberOfTestMethods)
            {
                batchSize = _numberOfTestMethods - NextStartIndex;
                batchSize = batchSize < 0 ? 0 : batchSize;
            }

            var batch = new TestBatch(NextStartIndex, batchSize);
            NextStartIndex += _batchSize;

            return batch;
        }
    }

    public class TestBatch
    {
        public int BatchSize { get; set; }
        public int Start { get; set; }

        public TestBatch(int start, int batchSize)
        {
            Start = start;
            BatchSize = batchSize;
        }

        public int End
        {
            get { return Start + BatchSize; }
        }

        public int Size
        {
            get { return End - Start; }
        }
    }
}