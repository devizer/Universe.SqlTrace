using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Universe.SqlTrace.LocalInstances
{
    [Serializable]
    public class LocalInstanceInfo
    {
        static readonly Version ZeroVersion = new Version(0,0,0,0);

        [XmlArray("SqlServers"), XmlArrayItem("Instance")]
        public List<SqlInstance> Instances = new List<SqlInstance>();

        public void SortByVersionAscending()
        {
            Instances.Sort((x, y) => (x.Ver ?? ZeroVersion).CompareTo(y.Ver ?? ZeroVersion));
        }

        public void SortByVersionDescending()
        {
            SortByVersionAscending();
            Instances.Reverse();
        }

        public void WriteToXml(TextWriter writer)
        {
            XmlTextWriter wr = new XmlTextWriter(writer);
            wr.Formatting = Formatting.Indented;
            XmlSerializer xs = new XmlSerializer(typeof(LocalInstanceInfo));
            xs.Serialize(writer, this);
            wr.Flush();
        }

        public override string ToString()
        {
            StringBuilder ret = new StringBuilder();
            foreach (SqlInstance instance in Instances)
            {
                if (ret.Length > 0)
                    ret.Append("; ");

                ret.Append(instance.IsDefault ? "(default)" : instance.Name);
                if (instance.Ver != null)
                    ret.Append(" ").Append(instance.Ver);

                ret.AppendFormat(" {{{0}}}", instance.ForecastLevelString);

                ret.Append(" ").Append(instance.Status);
            }

            return ret.ToString();
        }

        [Serializable]
        public class SqlInstance
        {
            [XmlAttribute]
            public string Name { get; set; }

            [XmlIgnore]
            public Version Ver { get; set; }

            [XmlIgnore]
            public Version FileVer { get; set; }

            [XmlAttribute("OK")]
            public bool IsOK { get; set; }

            [XmlAttribute("Edition")]
            public SqlEdition Edition { get; set; }

            [XmlAttribute("Description")]
            public string Description { get; set; }

            [XmlIgnore]
            public ServiceControllerStatus Status { get; set; }

            [XmlAttribute("Status")]
            public string StatusString
            {
                get { return (int) Status == 0 ? "" : Status.ToString(); }
                set { Status = string.IsNullOrEmpty(value) ? 0 : (ServiceControllerStatus)Enum.Parse(typeof(ServiceControllerStatus), value); }
            }

            [XmlAttribute("Version")]
            public string VerAsString
            {
                get { return Ver == null ? null : Ver.ToString(); }
                set { Ver = string.IsNullOrEmpty(value) ? null : new Version(value); }
            }

            [XmlAttribute("FileVersion")]
            public string FileVerAsString
            {
                get { return FileVer == null ? null : FileVer.ToString(); }
                set { FileVer = string.IsNullOrEmpty(value) ? null : new Version(value); }
            }

            public SqlInstance()
            {
            }


            public SqlInstance(string name)
            {
                Name = name;
            }

            public bool IsDefault
            {
                get { return string.IsNullOrEmpty(Name); }
            }

            public string FullLocalName
            {
                get { return IsDefault ? "(local)" : ("(local)" + "\\" + Name); }
            }

            public string ServiceKey
            {
                get { return GetServiceKey(Name); }
            }

            [XmlAttribute("Level")]
            public string Level { get; set; }

            public VersionLevel ForecastLevel
            {
                get
                {
                    Version ver = Ver ?? FileVer;
                    if (ver == null)
                        return VersionLevel.Unknown;

                    int mj = ver.Major, mn = ver.Minor, b = ver.Build;
                    if (mj == 8)
                    {
                        if (b < 194)
                            return VersionLevel.SQL2000 | VersionLevel.CTP;

                        else if (b < 384)
                            return VersionLevel.SQL2000 | VersionLevel.RTM;

                        else if (b < 532)
                            return VersionLevel.SQL2000 | VersionLevel.SP1;

                        else if (b < 760)
                            return VersionLevel.SQL2000 | VersionLevel.SP2;

                        else if (b < 2039)
                            return VersionLevel.SQL2000 | VersionLevel.SP3;

                        else
                            return VersionLevel.SQL2000 | VersionLevel.SP4;
                    }

                    else if (mj == 9)
                    {
                        if (b < 1399)
                            return VersionLevel.SQL2005 | VersionLevel.CTP;

                        else if (b < 2047)
                            return VersionLevel.SQL2005 | VersionLevel.RTM;

                        else if (b < 3042)
                            return VersionLevel.SQL2005 | VersionLevel.SP1;

                        else if (b < 4035)
                            return VersionLevel.SQL2005 | VersionLevel.SP2;

                        else if (b < 5000)
                            return VersionLevel.SQL2005 | VersionLevel.SP3;

                        else
                            return VersionLevel.SQL2005 | VersionLevel.SP4;
                    }

                    else if (mj == 10 && mn == 0)
                    {
                        if (b < 1600)
                            return VersionLevel.SQL2008 | VersionLevel.CTP;

                        if (b < 2531)
                            return VersionLevel.SQL2008 | VersionLevel.RTM;

                        if (b < 4000)
                            return VersionLevel.SQL2008 | VersionLevel.SP1;

                        if (b < 5500)
                            return VersionLevel.SQL2008 | VersionLevel.SP2;

                        else
                            return VersionLevel.SQL2008 | VersionLevel.SP3;

                        // SP3: 10.0.5500
                    }

                    else if (mj == 10 && (mn == 50 || mn == 51))
                    {
                        if (b < 1600)
                            return VersionLevel.SQL2008R2 | VersionLevel.CTP;

                        if (b < 2500)
                            return VersionLevel.SQL2008R2 | VersionLevel.RTM;

                        else
                            return VersionLevel.SQL2008R2 | VersionLevel.SP1;
                    }

                    else if (mj == 11)
                    {
                        // 2012: 11.0.2100.60
                        
                        if (b < 2100)
                            return VersionLevel.SQL2012 | VersionLevel.CTP;

                        else 
                            return VersionLevel.SQL2012 | VersionLevel.RTM;
                    }
                    

                    else
                        return VersionLevel.Unknown;
                }
            }

            public string ForecastLevelString
            {
                get
                {
                    var fl = ForecastLevel;
                    var fields = typeof(VersionLevel).GetFields(BindingFlags.Static | BindingFlags.Public);
                    foreach (var fi in fields)
                    {
                        if (fl.Equals(fi.GetValue(null)))
                        {
                            var attrs = fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                            if (attrs.Length > 0)
                                return ((DescriptionAttribute)attrs[0]).Description;
                        }
                    }

                    return fl.ToString();
                }
            }
        }


        public static string GetServiceKey(string instanceName)
        {
            return string.IsNullOrEmpty(instanceName) ? "MSSQLSERVER" : "MSSQL$" + instanceName; 
        }
    }

    [Flags]
    public enum VersionLevel
    {
        Unknown = 0,

        SQL2000 = 1,
        SQL2005 = 2,
        SQL2008 = 4,
        SQL2008R2 = 8,
        SQL2012 = 16,

        CTP = 1024,
        RTM = 1024 * 2,
        SP1 = 1024 * 4,
        SP2 = 1024 * 8,
        SP3 = 1024 * 16,
        SP4 = 1024 * 32,

        // 2000
        [Description("SQL Server 2000 CPT")]
        SQL2000CTP = SQL2000 | CTP,

        [Description("SQL Server 2000 RTM")]
        SQL2000RTM = SQL2000 | RTM,

        [Description("SQL Server 2000 SP1")]
        SQL2000SP1 = SQL2000 | SP1,

        [Description("SQL Server 2000 SP2")]
        SQL2000SP2 = SQL2000 | SP2,

        [Description("SQL Server 2000 SP3")]
        SQL2000SP3 = SQL2000 | SP3,

        [Description("SQL Server 2000 SP4")]
        SQL2000SP4 = SQL2000 | SP4,

        // 2005
        [Description("SQL Server 2005 CTP")]
        SQL2005CTP = SQL2005 | CTP,
        [Description("SQL Server 2005 RTM")]
        SQL2005RTM = SQL2005 | RTM,
        [Description("SQL Server 2005 SP1")]
        SQL2005SP1 = SQL2005 | SP1,
        [Description("SQL Server 2005 SP2")]
        SQL2005SP2 = SQL2005 | SP2,
        [Description("SQL Server 2005 SP3")]
        SQL2005SP3 = SQL2005 | SP3,
        [Description("SQL Server 2005 SP4")]
        SQL2005SP4 = SQL2005 | SP4,

        // 2008
        [Description("SQL Server 2008 CTP")]
        SQL2008CTP = SQL2008 | CTP,
        [Description("SQL Server 2008 RTM")]
        SQL2008RTM = SQL2008 | RTM,
        [Description("SQL Server 2008 SP1")]
        SQL2008SP1 = SQL2008 | SP1,
        [Description("SQL Server 2008 SP2")]
        SQL2008SP2 = SQL2008 | SP2,
        [Description("SQL Server 2008 SP3")]
        SQL2008SP3 = SQL2008 | SP3,

        // 2008 R2
        [Description("SQL Server 2008 R2 CTP")]
        SQL2008R2CTP = SQL2008R2 | CTP,
        [Description("SQL Server 2008 R2 RTM")]
        SQL2008R2RTM = SQL2008R2 | RTM,
        [Description("SQL Server 2008 R2 SP1")]
        SQL2008R2SP1 = SQL2008R2 | SP1,

        // 2012
        [Description("SQL Server 2012 CTP")]
        SQL2012CTP = SQL2012 | CTP,
        [Description("SQL Server 2012 RTM")]
        SQL2012RTM = SQL2012 | RTM,

    }

    public enum SqlEdition
    {
        Unknown,
        Express,
        LeastStandard,
    }
}