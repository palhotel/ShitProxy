using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ShitProxy
{
    class Program
    {
        public static void Proxy(string url, Socket socket)
        {
            WebRequest request = WebRequest.Create(url);
            try
            {
                WebResponse response = request.GetResponse();
                Stream stream = response.GetResponseStream();
                NetworkStream ns = new NetworkStream(socket);

                stream.CopyTo(ns);
                stream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "---" + e.StackTrace);
            }
            
            
            
        }

        public static void Listen()
        {
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            Int32 port = 1314;
            TcpListener listener = new TcpListener(localAddr, port);
            listener.Start();
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                NetworkStream ns = client.GetStream();
                StreamReader sr = new StreamReader(ns);
                string result;

                if (sr != null)
                {
                    try
                    {
                        result = sr.ReadLine();
                        while (!string.IsNullOrEmpty(result)) {
                            //Console.WriteLine(result);
                            if (result.Contains("HTTP/") && result.Contains("GET")) {
                                string pattern = @"[a-zA-z]+://[^\s]*";
                                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                                string url = result.Substring(regex.Match(result).Index, regex.Match(result).Length);
                                if (!string.IsNullOrEmpty(url)) {
                                    Proxy(url, client.Client);
                                }


                            }
                            result = sr.ReadLine();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + "---" + e.StackTrace);
                    }
                    
                   
                }
                client.Close();
            }
            listener.Stop();
           
        }

        static void Main(string[] args)
        {

            Thread th = new Thread(Listen);
            th.Start();
            Console.WriteLine("started proxy");
        }
    }
}
