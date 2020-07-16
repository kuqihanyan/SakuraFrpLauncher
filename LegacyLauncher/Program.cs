﻿using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.Security.Principal;
using System.Collections.Generic;
using System.Security.Cryptography;

using fastJSON;

using LegacyLauncher.Data;

namespace LegacyLauncher
{
    static class Program
    {
        public static readonly string ExecutablePath = Process.GetCurrentProcess().MainModule.FileName;
        public static readonly bool IsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        public static string AutoRunFile = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + @"\LegacySakuraLauncher_" + Md5(ExecutablePath) + ".lnk";
        public static string DefaultUserAgent = "SakuraLauncher/" + Assembly.GetExecutingAssembly().GetName().Version;
        
        public static Mutex AppMutex = null;

        #region Assistant Methods

        public static bool SetAutoRun(bool start)
        {
            try
            {
                if (start)
                {
                    if (File.Exists(AutoRunFile))
                    {
                        return true;
                    }
                    // Don't include IWshRuntimeLibrary here, IWshRuntimeLibrary.File will cause name conflict.
                    var shortcut = (IWshRuntimeLibrary.IWshShortcut)new IWshRuntimeLibrary.WshShell().CreateShortcut(AutoRunFile);
                    shortcut.TargetPath = ExecutablePath;
                    shortcut.Arguments = "--minimize";
                    shortcut.Description = "SakuraFrp Legacy Launcher Auto Start";
                    shortcut.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    shortcut.Save();
                }
                else if (File.Exists(AutoRunFile))
                {
                    File.Delete(AutoRunFile);
                }
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("无法设置开机启动, 请检查杀毒软件是否拦截了此操作.\n\n" + e.ToString(), "Oops", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        public static Dictionary<string, dynamic> ApiRequest(string action, string query = null)
        {
            try
            {
                var json = JSON.ToObject<Dictionary<string, object>>(HttpGetString("https://api.natfrp.com/client/" + action + "?token=" + MainForm.Instance.UserToken.Trim() + (query == null ? "" : "&" + query)));
                if ((bool)json["success"])
                {
                    return json;
                }
                MessageBox.Show(json["message"] as string ?? "出现未知错误", "Oops", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception e)
            {
                MessageBox.Show("无法完成请求, 请检查网络连接并重试\n\n" + e.ToString(), "Oops", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null;
        }

        public static string HttpGetString(string url, Encoding encoding = null, int timeoutMs = 5000, IWebProxy proxy = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            return encoding.GetString(HttpGetBytes(url, timeoutMs, proxy));
        }

        public static byte[] HttpGetBytes(string url, int timeoutMs = -1, IWebProxy proxy = null)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768); // Tls12 & Tls11
            if (url.StartsWith("//"))
            {
                url = "https:" + url;
            }
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = DefaultUserAgent;
            request.Credentials = CredentialCache.DefaultCredentials;
            if (proxy != null)
            {
                request.Proxy = proxy;
            }
            if (timeoutMs > 0)
            {
                request.Timeout = timeoutMs;
            }
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Bad HTTP Status(" + url + "):" + response.StatusCode + " " + response.StatusDescription);
                }
                using (var ms = new MemoryStream())
                {
                    int count;
                    byte[] buffer = new byte[4096];
                    var stream = response.GetResponseStream();
                    while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        ms.Write(buffer, 0, count);
                    }
                    return ms.ToArray();
                }
            }
        }

        public static string Md5(byte[] data)
        {
            try
            {
                StringBuilder Result = new StringBuilder();
                foreach (byte Temp in new MD5CryptoServiceProvider().ComputeHash(data))
                {
                    if (Temp < 16)
                    {
                        Result.Append("0");
                        Result.Append(Temp.ToString("x"));
                    }
                    else
                    {
                        Result.Append(Temp.ToString("x"));
                    }
                }
                return Result.ToString();
            }
            catch
            {
                return "0000000000000000";
            }
        }

        public static string Md5(string Data) => Md5(EncodeByteArray(Data));

        public static byte[] EncodeByteArray(string data) => data == null ? null : Encoding.UTF8.GetBytes(data);

        #endregion

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!File.Exists(Tunnel.ClientPath))
            {
                MessageBox.Show("未找到 frpc.exe, 请尝试重新下载客户端.", "Oops", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }

            var minimize = false;
            foreach (var a in args)
            {
                var split = a.Split('=');
                if (split[0] == "--minimize")
                {
                    minimize = true;
                }
            }

            AppMutex = new Mutex(true, "LegacySakuraLauncher_" + Md5(Path.GetFullPath("config.json")), out bool created);

            if (created)
            {
                Application.Run(new MainForm(minimize));
            }
            else
            {
                MessageBox.Show("请不要重复开启 SakuraFrp 客户端. 如果想运行多个实例请将软件复制到其他目录.", "Oops", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            AppMutex.ReleaseMutex();
        }
    }
}
