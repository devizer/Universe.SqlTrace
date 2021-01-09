using System;
using System.Collections.Generic;

namespace Universe.SqlTrace
{
    public class TraceRowFilter
    {
        private TraceColumns _column;
        private object _value;
        private bool _strictEquality = true;

        public TraceRowFilter(TraceColumns column, object value)
        {
            _column = column;
            _value = value;
        }

        public TraceRowFilter(TraceColumns column, object value, bool strictEquality)
        {
            _column = column;
            _value = value;
            _strictEquality = strictEquality;
        }

        public TraceColumns Column
        {
            get { return _column; }
        }

        public object Value
        {
            get { return _value; }
        }

        public bool StrictEquality
        {
            get { return _strictEquality; }
        }

        public static TraceRowFilter CreateByDatabase(string databaseName)
        {
            CheckArg("databaseName", databaseName);
            return new TraceRowFilter(TraceColumns.Database, databaseName);
        }

        public static TraceRowFilter CreateByApplication(string applicationName)
        {
            CheckArg("applicationName", applicationName);
            return new TraceRowFilter(TraceColumns.Application, applicationName);
        }

        public static TraceRowFilter CreateLikeApplication(string applicationName)
        {
            CheckArg("applicationName", applicationName);
            var ret = new TraceRowFilter(TraceColumns.Application, applicationName, false);
            return ret;
        }

        public static TraceRowFilter CreateByClientProcess(int idClientProcess)
        {
            CheckArg("idClientProcess", idClientProcess);
            return new TraceRowFilter(TraceColumns.ClientProcess, idClientProcess);
        }

        public static TraceRowFilter CreateByClientHost(string clientHost)
        {
            CheckArg("clientHost", clientHost);
            return new TraceRowFilter(TraceColumns.ClientHost, clientHost);
        }

        public static TraceRowFilter CreateByLogin(string login)
        {
            CheckArg("login", login);
            return new TraceRowFilter(TraceColumns.Login, login);
        }
        
        public static TraceRowFilter CreateByServerProcess(int idServerProcess)
        {
            CheckArg("idServerProcess", idServerProcess);
            return new TraceRowFilter(TraceColumns.ServerProcess, idServerProcess);
        }

        static void CheckArg(string name, string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Parameter " + name + " value is missing", name);
        }

        static void CheckArg(string name, int value)
        {
            if (value == 0)
                throw new ArgumentException("Parameter " + name + " value is missing", name);
        }

        public static TraceRowFilter[] GetDistinct(TraceRowFilter[] arg)
        {
            Dictionary<string, TraceRowFilter> unique = new Dictionary<string, TraceRowFilter>();
            foreach (TraceRowFilter rowFilter in arg)
                unique[rowFilter.Column + ":" + rowFilter.Value] = rowFilter;

            return new List<TraceRowFilter>(unique.Values).ToArray();
        }
    }
}
