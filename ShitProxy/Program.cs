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
        public delegate void ProxyDoneEventHandler(object sendor, EventArgs e);

        public static event ProxyDoneEventHandler ProxyDone;

        //Todo:Asynchronize
        public static  void Proxy(string url, TcpClient client)
        {
            WebRequest request = WebRequest.Create(url);
            var socket = client.Client;
            socket.ReceiveBufferSize = 65536;
            socket.SendBufferSize = 65536;
            request.Timeout = 30000;
            WebResponse response = null;
            Stream stream = null;
            try
            {
                NetworkStream targetStream = new NetworkStream(socket);
                response = request.GetResponse();
                stream = response.GetResponseStream();
                if (stream == null)
                {
                    throw new Exception("response.GetResponseStream() failed");
                }
                //sendto the ip and port

                stream.CopyTo(targetStream);
                if (ProxyDone != null)
                {
                    ProxyDone(client, new EventArgs());
                }
            }
            catch (Exception e)
            {
                if (ProxyDone != null) {
                    ProxyDone(client, new EventArgs());
                }
                throw new Exception(e.Message);
            }
            
        }

        public static void Listen()
        {
            IPAddress localAddr = IPAddress.Parse("0.0.0.0");
            Int32 port = 1314;
            TcpListener listener = new TcpListener(localAddr, port);
            listener.Start();
            while (true)
            {
                //blocked, wait for a request, a request may fetch one file once a time
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("#new tcp client connected");
                NetworkStream ns = client.GetStream();
                StreamReader sr = new StreamReader(ns);
                string result;
                try
                {
                    //support GET, only handle url
                    List<string> urls = new List<string>();
                    while (sr.Peek() >= 0)
                    {
                        result = sr.ReadLine();
                        if (!string.IsNullOrEmpty(result) && result.Contains("HTTP/") && result.StartsWith("GET")) {
                            string pattern = @"[a-zA-z]+://[^\s]*";
                            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                            string url = result.Substring(regex.Match(result).Index, regex.Match(result).Length);
                            //File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory + "log.txt", url);
                            urls.Add(url);
                        }
                    }
                    Console.WriteLine(urls.Count);
                    foreach( string url in urls)
                    {
                        Proxy(url, client);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "---" + e.StackTrace);
                    if (ProxyDone != null) {
                        ProxyDone(client, new EventArgs());
                    }
                }
            }
           
        }

        static void Main(string[] args)
        {
            ProxyDone += new ProxyDoneEventHandler((sender, e) =>
            {
                if (sender != null)
                {
                    Console.WriteLine("#Close client");
                    var client = (TcpClient)sender;
                    client.Close();
                }
            });
            Thread th = new Thread(Listen);
            th.Start();
            Console.WriteLine("started proxy");
        }
    }
}
