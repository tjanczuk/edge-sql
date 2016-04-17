using System;
using System.Configuration;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace unitTest {

    [TestFixture]
    public class UnitTest1 {
        int open() {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["connectionString"] = getConnectionString();
            param["driver"] = getDriver();
            param["cmd"] = "open";
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var res = t.Invoke(null);
            Task.WaitAll(res);
            return (int) res.Result;
        }

        void close(int handler) {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["cmd"] = "close";
            param["handler"] = handler;
            param["driver"] = getDriver();
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            Task<object> resClose =  t.Invoke(null);
            Task.WaitAll(resClose);
        }

        string getConnectionString() {
            string connName = "mySql";
            if (System.Environment.GetEnvironmentVariable("travis") != null) {
                connName = "travis";
            }
            return ConfigurationManager.ConnectionStrings[connName].ConnectionString;
        }

        string getDriver() {
            string connName = "mySql";
            if (System.Environment.GetEnvironmentVariable("travis") != null) {
                connName = "travis";
            }
            return ConfigurationManager.AppSettings["driver." + connName];
        }

        [Test]
        public void compilerExists() {

            EdgeCompiler ec = new EdgeCompiler();
            Assert.IsTrue(ec.GetType().GetMethod("CompileFunc") != null, "EdgeCompiler has CompileFunc");

        }


        [Test]
        public void openConnection() {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["connectionString"] = getConnectionString();
            param["driver"] = getDriver();
            param["cmd"] = "open";
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var res = t.Invoke(null);
            Task.WaitAll(res);
            Assert.AreEqual(res.Status, TaskStatus.RanToCompletion, "Open executed");
            Assert.IsFalse(res.IsFaulted);
            Assert.IsInstanceOf(typeof (int), res.Result,"Open returned an int");           
        }

        [Test]
        public void openBadConnection() {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["connectionString"] = "bad connection";
            param["driver"] = getDriver();
            param["cmd"] = "open";
            param["timeout"] = 3;
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            Task <object> res=null;
            try {
                res = t.Invoke(null);
                Task.WaitAll(res);
                Assert.IsFalse(res.IsFaulted,"Open bad connection should throw");
            }
            catch {
                Assert.IsNotNull(res, "Open task should exist");
                Assert.AreEqual(TaskStatus.Faulted, res.Status, "Open bad connection should throw");                
            }
        }

        [Test]
        public void closeConnection() {
            int handler = open();

            Dictionary<string, object>  param = new Dictionary<string, object>();
            param["cmd"] = "close";
            param["handler"] = handler;
            param["driver"] = getDriver();
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);            
            try {
                var resClose = t.Invoke(null);
                Task.WaitAll(resClose);
                Assert.IsFalse(resClose.IsFaulted, "Close connection should success");
            }
            catch {
                Assert.AreEqual(true,false, "Close connection should not throw");
            }

        }

        [Test]
        public void setupScriptShouldExist() {
            Assert.IsTrue(File.Exists("setup.sql"), "setup script should be present");
        }

        [Test]
        public void getDbDate() {
            int handler = open();

            Dictionary<string, object> param = new Dictionary<string, object> {
                ["cmd"] = "nonquery",
                ["handler"] = handler,
                ["driver"] = getDriver()
            };
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);



            close(handler);
        }
    }
}
