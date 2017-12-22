using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NeneNeko.NovelTools
{

    public static class Helper
    {

        public static string[] Explode(this string str, string split_by = ",")
        {
            return str.Split(new string[] { split_by }, StringSplitOptions.None);
        }

        public static string GetHostName(this string url)
        {
            Uri HostName = new Uri(url);
            return HostName.Host.Replace("www.", string.Empty);
        }

        public static String BytesToString(this long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public static void ErrorLogging(Exception ex)
        {
            string strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorLog.txt");
            if (!File.Exists(strPath))
            {
                File.Create(strPath).Dispose();
            }
            using (StreamWriter sw = File.AppendText(strPath))
            {
                sw.WriteLine("=============Error Logging ===========");
                sw.WriteLine("===========Start============= " + DateTime.Now);
                sw.WriteLine("Error Message: " + ex.Message);
                sw.WriteLine("Stack Trace: " + ex.StackTrace);
                sw.WriteLine("===========End============= " + DateTime.Now);

            }
        }

        public static String ExecCommand(string filename, string arguments)
        {
            Process process = new Process();
            ProcessStartInfo psi = new ProcessStartInfo(filename);
            psi.Arguments = arguments;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            process.StartInfo = psi;
            StringBuilder output = new StringBuilder();
            process.OutputDataReceived += (sender, e) => { output.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { output.AppendLine(e.Data); };
            // run the process
            process.Start();
            // start reading output to events
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            // wait for process to exit
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception("Command " + psi.FileName + " returned exit code " + process.ExitCode);
            }
            return output.ToString();
        }

    }

    public static class ForEachHelper
    {
        public sealed class Item<T>
        {
            public int Index { get; set; }
            public T Value { get; set; }
            public bool IsLast { get; set; }
        }

        public static IEnumerable<Item<T>> WithIndex<T>(IEnumerable<T> enumerable)
        {
            Item<T> item = null;
            foreach (T value in enumerable)
            {
                Item<T> next = new Item<T>();
                next.Index = 0;
                next.Value = value;
                next.IsLast = false;
                if (item != null)
                {
                    next.Index = item.Index + 1;
                    yield return item;
                }
                item = next;
            }
            if (item != null)
            {
                item.IsLast = true;
                yield return item;
            }
        }
    }

    public abstract class AppConfig : IDisposable
    {
        public static AppConfig Change(string path)
        {
            return new ChangeAppConfig(path);
        }

        public abstract void Dispose();

        private class ChangeAppConfig : AppConfig
        {
            private readonly string oldConfig =
                AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();

            private bool disposedValue;

            public ChangeAppConfig(string path)
            {
                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", path);
                ResetConfigMechanism();
            }

            public override void Dispose()
            {
                if (!disposedValue)
                {
                    AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", oldConfig);
                    ResetConfigMechanism();
                    disposedValue = true;
                }
                GC.SuppressFinalize(this);
            }

            private static void ResetConfigMechanism()
            {
                typeof(ConfigurationManager)
                    .GetField("s_initState", BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, 0);

                typeof(ConfigurationManager)
                    .GetField("s_configSystem", BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, null);

                typeof(ConfigurationManager)
                    .Assembly.GetTypes()
                    .Where(x => x.FullName == "System.Configuration.ClientConfigPaths").First()
                    .GetField("s_current", BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, null);
            }
        }
    }

}
