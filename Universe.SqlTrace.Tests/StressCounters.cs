namespace Universe.SqlTrace.Tests
{
    internal class StressCounters
    {
        public long
            Client_CPU,
            Client_Time,
            Server_CPU,
            Server_Duration,
            Server_Reads,
            Server_Writes;

        public override string ToString()
        {
            return string.Format("Client CPU: {0}, Client Time: {1}, Server CPU: {2}, Server Duration: {3}, Server Reads: {4}, Server Writes: {5}", Client_CPU, Client_Time, Server_CPU, Server_Duration, Server_Reads, Server_Writes);
        }

        public void Add(StressCounters other)
        {
            lock (this)
            {
                Client_CPU += other.Client_CPU;
                Client_Time += other.Client_Time;
                Server_CPU += other.Server_CPU;
                Server_Duration += other.Server_Duration;
                Server_Reads += other.Server_Reads;
                Server_Writes += other.Server_Writes;
            }
        }

        public StressCounters Clone()
        {
            lock (this)
            {
                StressCounters ret = new StressCounters();
                ret.Client_CPU = Client_CPU;
                ret.Client_Time = Client_Time;
                ret.Server_CPU = Server_CPU;
                ret.Server_Duration = Server_Duration;
                ret.Server_Reads = Server_Reads;
                ret.Server_Writes = Server_Writes;
                return ret;
            }
        }
    }
}