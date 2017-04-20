using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Universe.SqlTrace.LocalInstances;

namespace Universe.SqlTrace.Tests
{
    [TestFixture]
    public class Test_Local_Instances_Discovery
    {

        [Test]
        public void Local_Instances_Get()
        {
            var servers = LocalInstancesDiscovery.Get();
        }

        [Test]
        public void Local_Instances_GetFull()
        {
            var servers = LocalInstancesDiscovery.GetFull(TimeSpan.FromSeconds(9));
        }

        [Test]
        public void Local_Instances_Contains_Data()
        {
            var servers = LocalInstancesDiscovery.Get();
            StringBuilder dump = new StringBuilder();
            var stringWriter = new StringWriter(dump);
            servers.WriteToXml(stringWriter);
            Trace.WriteLine("FOUND SQL Instances:" + Environment.NewLine + dump);

            Assert.IsTrue(servers.Instances.Count > 0, "SQL Server is required. Either running or stopped");
            foreach (var i in servers.Instances)
            {
                Assert.IsNotNull(i.FileVer, "File version property of instance {0} is required", i);
                Assert.IsTrue(i.FileVer.Major != 0, "Major file version of instance {0} should be not zero", i);
                Assert.IsNotNull(i.Name, "Instance should have name");
            }
        }


    }
}
