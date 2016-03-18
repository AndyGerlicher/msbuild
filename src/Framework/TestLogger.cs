using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Microsoft.Build.Framework
{
    public class TestLogger
    {
        public static readonly TestLogger Instance = new TestLogger();

        private readonly string _file;
        private readonly StringBuilder _sb = new StringBuilder();
        private bool _dirty = false;
        private object _lock = new object();

        public TestLogger()
        {
            int max = 1;
            var root = new DirectoryInfo(@"c:\debugdump");

            foreach (var folder in root.GetDirectories())
            {
                int i;
                if (!Int32.TryParse(folder.Name, out i)) continue;

                if (i > max)
                    max = i;
            }

            max++;

            var folder1 = Path.Combine(root.FullName, max.ToString());
            Directory.CreateDirectory(folder1);
            _file = Path.Combine(folder1, "log.txt");

            Thread t = new Thread((o) =>
            {
                while (true)
                {
                    Thread.Sleep(10000);
                    Save();
                }
            });
            t.Start();
        }

        public void Log(string message, IEnumerable<Tuple<string, object>> objects)
        {
            if (!File.Exists(@"C:\debugdump\debug.enabled")) return;

            lock (_lock)
            {
                _dirty = true;

                var method = String.Empty;
                var stackMessage = String.Empty;
                string stackMessage2 = String.Empty;

                try
                {
                    var stack = new StackTrace().GetFrames().Skip(2);
                    var caller = stack.First();

                    method = caller.GetMethod().Name;
                    stackMessage = "  Stack: " + JsonConvert.SerializeObject(stack.Select(f => $"{f.GetMethod().Name}"));
                    stackMessage2 = "  Full Stack: " + Environment.StackTrace.Replace(Environment.NewLine, String.Empty);
                    //+ JsonConvert.SerializeObject(stack.Select(f => $"{f.ToString()}"));
                }
                catch (Exception e)
                {
                    _sb.AppendLine($"  Ex (stack): {e}");
                }

                if (!String.IsNullOrEmpty(message))
                {
                    _sb.AppendLine(message);
                    if (!message.StartsWith(method))
                        _sb.AppendLine(method);
                }
                else
                {
                    _sb.AppendLine(method);
                }

                if (objects != null)
                {
                    try
                    {
                        foreach (var o in objects)
                        {
                            _sb.AppendLine($"  {o.Item1}: " + JsonConvert.SerializeObject(o.Item2));
                        }
                    }
                    catch (Exception e)
                    {
                        _sb.AppendLine($"  Ex: {e}");
                    }
                }

                _sb.AppendLine(stackMessage);
                _sb.AppendLine(stackMessage2);
                _sb.AppendLine($"  Thread: Id {Thread.CurrentThread.ManagedThreadId}, Name {Thread.CurrentThread.Name}");
                _sb.AppendLine("---");
            }
        }

        private void Save()
        {
            if (!_dirty) return;

            lock (_lock)
            {
                File.WriteAllText(_file, _sb.ToString());
                _dirty = false;
            }
        }

        public static void TestLog(string message, List<Tuple<string, object>> objects)
        {
            Instance.Log(message, objects);
        }

        public static void TestLog(params Tuple<string, object>[] objects)
        {
            Instance.Log(String.Empty, objects);
        }

        public static void TestLog(string message, params Tuple<string, object>[] objects)
        {
            Instance.Log(message, objects);
        }

        public static void TestLog()
        {
            Instance.Log(null, null);
        }

        public static void TestLog(string message)
        {
            Instance.Log(message, null);
        }

        public static void TestLog(Exception ex)
        {
            TestLogger.Instance.Log($"ERROR - {ex.Message}",
                new List<Tuple<string, object>> { new Tuple<string, object>("ex", ex) });
        }
    }
}