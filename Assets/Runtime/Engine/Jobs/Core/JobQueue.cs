using Runtime.Engine.ThirdParty.Priority_Queue;

namespace Runtime.Engine.Jobs.Core
{
    /// <summary>
    /// Generic job queue for scheduling, processing, completing, and cleaning up jobs.
    /// </summary>
    public class JobQueue<T>
    {
        private SimplePriorityQueue<T> _claimQueue;
        private SimplePriorityQueue<T> _reclaimQueue;

        public JobQueue()
        {
            _claimQueue = new SimplePriorityQueue<T>();
            _reclaimQueue = new SimplePriorityQueue<T>();
        }

        /// <summary>
        /// Schedule something for computation
        /// </summary>
        public void Schedule()
        {
        }

        /// <summary>
        /// Take items for queue and send for processing
        /// </summary>
        public void Process()
        {
        }

        /// <summary>
        /// item has been processed
        /// </summary>
        public void Complete()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clean()
        {
        }
    }
}