﻿/* 27 Oct 2014
// This code released to community under The Code Project Open License (CPOL) 1.02
// The copyright owner and author of this version of the code is Robert Ellis
// Please retain this notice and clearly identify your own edits, amendments and/or contributions
// In line with the CPOL this code is provided "AS IS" and without warranty
// Use entirely at your own risk
*/

namespace ServiceTimer
{
    using System;
    using System.Text;

    public sealed class TimerWorkerInfo
    {
        private TimerWorkerInfo()
        {
            // Hide the default constructor
        }

        public uint ElapseCount { get; private set; }
        public int ThreadId { get; private set; }
        public double TimerInterval { get; private set; }
        public ulong TotalElapseCount { get; private set; }
        public Guid WorkId { get; private set; }
        public uint WorkOnElapseCount { get; private set; }

        internal static TimerWorkerInfo Info(double timerInterval, uint elapseCount, uint workOnElapseCount, ulong totalElapseCount)
        {
            var i = new TimerWorkerInfo
            {
                TimerInterval = timerInterval,
                ElapseCount = elapseCount,
                WorkOnElapseCount = workOnElapseCount,
                TotalElapseCount = totalElapseCount,
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                WorkId = Guid.NewGuid()
            };

            return i;
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.Append($"TimerInterval: {TimerInterval}");
            s.Append($" TotalElapseCount: {TotalElapseCount}");
            s.Append($" ThreadId: {ThreadId}");
            s.Append($" WorkId: {WorkId}");

            return s.ToString();
        }
    }
}