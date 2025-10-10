using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Runtime.Engine.Jobs.Core {

    public abstract class JobScheduler {

        private Queue<long> _timings;
        private Stopwatch _watch;
        private int _records;

        protected JobScheduler(int records = 16) {
            _records = records;
            _watch = new Stopwatch();
            _timings = new Queue<long>(_records);
        }

        public float AvgTime => (float) _timings.Sum() / 10;

        protected void StartRecord() {
            _watch.Restart();
        }
        
        protected void StopRecord() {
            _watch.Stop();
            long ms = _watch.ElapsedMilliseconds;
            
            if (_timings.Count <= _records) _timings.Enqueue(ms);
            else {
                _timings.Dequeue();
                _timings.Enqueue(ms);
            }
        }
        
    }

}