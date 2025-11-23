using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Runtime.Engine.Jobs.Core
{
    /// <summary>
    /// Abstrakte Basis für Scheduler mit Timing-Aufzeichnung (gleitender Durchschnitt über letzte N Ausführungen).
    /// </summary>
    public abstract class JobScheduler
    {
        private readonly Queue<long> _timings;
        private readonly Stopwatch _watch;
        private readonly int _records;

        /// <summary>
        /// Erstellt Scheduler mit definierter Anzahl Historien-Einträge.
        /// </summary>
        protected JobScheduler(int records = 16)
        {
            _records = records;
            _watch = new Stopwatch();
            _timings = new Queue<long>(_records);
        }

        /// <summary>
        /// Durchschnittliche Zeit der letzten aufgezeichneten Durchläufe (ms/10 - Skalierung beibehalten?).
        /// </summary>
        public float AvgTime => (float)_timings.Sum() / 10;

        /// <summary>
        /// Startet Zeitaufzeichnung für aktuellen Lauf.
        /// </summary>
        protected void StartRecord()
        {
            _watch.Restart();
        }

        /// <summary>
        /// Stoppt Zeitaufzeichnung und legt Messwert in Historie ab.
        /// </summary>
        protected long StopRecord()
        {
            _watch.Stop();
            long ms = _watch.ElapsedMilliseconds;

            if (_timings.Count > _records)
            {
                _timings.Dequeue();
            }

            _timings.Enqueue(ms);
            return ms;
        }
    }
}