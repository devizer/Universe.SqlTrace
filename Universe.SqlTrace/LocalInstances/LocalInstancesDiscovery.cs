using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Universe.SqlTrace.LocalInstances
{
    public class LocalInstancesDiscovery
    {
        
/*
        static LocalInstancesDiscovery()
        {
            try
            {
                LocalInstanceInfo i = new LocalInstanceInfo();
                StringBuilder dump = new StringBuilder();
                var stringWriter = new StringWriter(dump);
                i.WriteToXml(stringWriter);
                Trace.WriteLine("");
            }
            catch (Exception)
            {
            }
        }
*/
        
        public static LocalInstanceInfo GetFull(TimeSpan timeout)
        {
            LocalInstanceInfo ret = new LocalInstanceInfo();
            GetList(ret);
            BuildDescription(ret, timeout);
            ret.SortByVersionDescending();
            return ret;
        }

        public static LocalInstanceInfo Get()
        {
            LocalInstanceInfo ret = new LocalInstanceInfo();
            GetList(ret);
            var filtered = new List<LocalInstanceInfo.SqlInstance>();
            foreach (LocalInstanceInfo.SqlInstance i in ret.Instances)
            {
                string path;
                Version version;
                ServiceControllerStatus status;
                if (TryService(LocalInstanceInfo.GetServiceKey(i.Name), out path, out version, out status))
                {
                    i.Status = status;
                    filtered.Add(i);
                }
            }

            ret.Instances.Clear();
            ret.Instances.AddRange(filtered);

            ret.SortByVersionDescending();
            return ret;
        }

        private static void GetList(LocalInstanceInfo ret)
        {
            using (RegistryKey lm = Registry.LocalMachine)
            {
                // default instance
                using (RegistryKey k0 = lm.OpenSubKey(@"SOFTWARE\Microsoft\MSSQLServer"))
                    if (k0 != null)
                        TryKey(k0, ret, string.Empty);

                // named instances
                using (RegistryKey k1 = lm.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server", false))
                    if (k1 != null)
                        foreach (string subKeyName in new List<string>(k1.GetSubKeyNames() ?? new string[0]))
                            using (RegistryKey candidate = k1.OpenSubKey(subKeyName))
                                if (candidate != null)
                                    TryKey(candidate, ret, subKeyName);
            }
        }



        private static void TryKey(RegistryKey k1, LocalInstanceInfo ret, string instanceName)
        {
            string rawVersion = null;
            using (RegistryKey rk = k1.OpenSubKey(@"MSSQLServer\CurrentVersion", false))
                if (rk != null)
                    rawVersion = rk.GetValue("CurrentVersion") as string;

/*
            string rawPath = null;
            using (RegistryKey rk = k1.OpenSubKey(@"Setup", false))
                if (rk != null)
                    rawPath = rk.GetValue("SQLPath") as string;
*/


            if (!string.IsNullOrEmpty(rawVersion) /* && rawPath != null && Directory.Exists(rawPath) */)
            {
                try
                {
                    Version ver = new Version(rawVersion);
                    var i = new LocalInstanceInfo.SqlInstance(instanceName);
                    i.FileVer = ver;
                    ret.Instances.Add(i);
                }
                catch
                {
                }
            }
        }

        static void BuildDescription(LocalInstanceInfo info, TimeSpan timeout)
        {
            if (info.Instances.Count > 0)
            {
                List<ManualResetEvent> events = new List<ManualResetEvent>();
                foreach (LocalInstanceInfo.SqlInstance instance in info.Instances)
                {
                    // description
                    LocalInstanceInfo.SqlInstance sqlInstance = instance;
                    ManualResetEvent ev = new ManualResetEvent(false);
                    events.Add(ev);
                    ThreadPool.QueueUserWorkItem(
                        delegate
                            {
                                try
                                {
                                    // Console.WriteLine(i2.FullLocalName + ": " + Thread.CurrentThread.ManagedThreadId);
                                    // Thread.Sleep(3000);
                                    string fullPath;
                                    Version productVersion;
                                    ServiceControllerStatus status;
                                    if (TryService(LocalInstanceInfo.GetServiceKey(sqlInstance.Name), out fullPath, out productVersion, out status))
                                    {
                                        sqlInstance.Status = status;
                                        sqlInstance.Ver = productVersion;

                                        if (sqlInstance.Status == ServiceControllerStatus.Running)
                                        {
                                            string cs = "Data Source=" + sqlInstance.FullLocalName + ";Integrated Security=SSPI;Pooling=False;";
                                            using (SqlConnection con = new SqlConnection(cs))
                                            using (SqlCommand cmd = new SqlCommand("Select @@version, SERVERPROPERTY('ProductLevel'), SERVERPROPERTY('Edition')", con))
                                            {
                                                cmd.CommandTimeout = (int) timeout.TotalSeconds;
                                                con.Open();
                                                using (SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
                                                {
                                                    if (rdr.Read())
                                                    {
                                                        string description = (string) rdr.GetString(0);
                                                        string level = (string) rdr.GetString(1);
                                                        string edition = (string) rdr.GetString(2);

                                                        description =
                                                            description.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                                                                
                                                        while (description.IndexOf("  ") >= 0)
                                                            description = description.Replace("  ", " ");

                                                        sqlInstance.IsOK = true;
                                                        sqlInstance.Description = description;
                                                        sqlInstance.Level = level;
                                                        bool isExpress = 
                                                            edition.IndexOf("Desktop Engine") >= 0
                                                            || edition.IndexOf("Express Edition") >= 0;

                                                        sqlInstance.Edition = isExpress ? SqlEdition.Express : SqlEdition.LeastStandard;
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(
                                        "Failed to connect to " + sqlInstance.FullLocalName + ". See Details Below" + Environment.NewLine +
                                        ex + Environment.NewLine);
                                }
                                finally
                                {
                                    ev.Set();
                                }
                            });
                } // done: description


                WaitHandle.WaitAll(events.ToArray());

                List<LocalInstanceInfo.SqlInstance> normal = info.Instances.FindAll(delegate(LocalInstanceInfo.SqlInstance i) { return i.Ver != null; });
                info.Instances.Clear();
                info.Instances.AddRange(normal);

            }
        }


        private static bool TryService(string serviceKey, out string fullPath, out Version productVersion, out ServiceControllerStatus status)
        {
            fullPath = null;
            productVersion = null;
            status = 0;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceKey, false))
                {
                    if (key != null)
                    {
                        object obj2 = key.GetValue("ImagePath");
                        string fp = (obj2 == null) ? null : obj2.ToString().Trim(new char[] { ' ' });
                        if (!string.IsNullOrEmpty(fp))
                        {
                            if (fp.StartsWith("\""))
                            {
                                int p = fp.IndexOf('\"', 1);
                                if (p >= 0)
                                    fp = fp.Substring(1, p - 1);
                            }
                            else
                            {
                                // MSDE FIX
                                int p2 = fp.LastIndexOf(' ');
                                if (p2 > 0 && p2 < fp.Length - 1 && fp.Substring(p2).Trim().StartsWith("-s"))
                                {
                                    string tryFp = fp.Substring(0, p2);
                                    if (File.Exists(tryFp))
                                        fp = tryFp;
                                }
                            }

                            fullPath = fp;
                            productVersion = new Version(FileVersionInfo.GetVersionInfo(fullPath).ProductVersion);
                        }
                    }
                }

                if (fullPath == null)
                    return false;

                ServiceController controller = new ServiceController(serviceKey);
                try
                {
                    controller.Refresh();
                    status = controller.Status;
                    return true;
                }
                catch(InvalidOperationException ex)
                {
                    Win32Exception iex = ex.InnerException as Win32Exception;
                    if (iex != null && iex.NativeErrorCode == 1060)
                        return false;
                        
                    else throw;
                }

            }
            catch(Exception ex)
            {
                Debug.WriteLine("Failed to get info of service " + serviceKey + ". See Details Below" + Environment.NewLine + ex + Environment.NewLine);

                return false;
            }
        }
    }
}