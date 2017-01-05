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
        public static void Proxy(string requestPacket, TcpClient client)
        {
            if (string.IsNullOrEmpty(requestPacket))
            {
                return;
            }

            var arr = requestPacket.Replace("Proxy-Connection", "Connection").Split('\n');
            string url = "";
            if (arr[0].StartsWith("GET"))
            {
                url = arr[0].Split(' ')[1];
            }
            else if(arr[0].StartsWith("CONNECT"))
            {
                url = "https://" + arr[0].Split(' ', ':')[1];
            }

            HttpWebRequest request = (HttpWebRequest) HttpWebRequest.Create(url);

            request.Timeout = 65536;

            HttpWebResponse response = null;
            WebResponse wr = null;
            Stream stream = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                stream = response.GetResponseStream();
                Stream targetStream = client.GetStream();

                Console.WriteLine("got response from target Server ");
                if (stream == null)
                {
                    throw new Exception("response.GetResponseStream() failed");
                }

                //Write Headers
                var httpFirst = "HTTP/1.1 200 OK\r\n";
                targetStream.Write(Encoding.ASCII.GetBytes(httpFirst), 0, httpFirst.Length);
                var headerStr = response.Headers.ToString();
                targetStream.Write(Encoding.ASCII.GetBytes(headerStr), 0, headerStr.Length);
                //body
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

        private static void HandleRequest(object state)
        {
            var st = (TcpState) state;
            var client = st.client;

            NetworkStream ns = client.GetStream();
            StreamReader sr = new StreamReader(ns);
            string result = "";
            try {
                while (sr.Peek() >= 0)
                {
                    result = result + sr.ReadLine() + "\n";
                }
                //Console.WriteLine(result);
                Proxy(result.TrimEnd(), client);
            }
            catch (Exception e) {
                Console.WriteLine(e.Message + "---" + e.StackTrace);
                if (ProxyDone != null) {
                    ProxyDone(client, new EventArgs());
                }
            }
        }

        private class TcpState
        {
            public TcpClient client;

            public TcpState(TcpClient client)
            {
                this.client = client;
            }
        }

        public static void Listen()
        {
            IPAddress localAddr = IPAddress.Parse("0.0.0.0");
            Int32 port = 1314;
            TcpListener listener = new TcpListener(localAddr, port);
            listener.Start();
            ThreadPool.SetMaxThreads(25, 50);
            while (true)
            {
                //blocked, wait for a request
                TcpClient client = listener.AcceptTcpClient();
                client.SendTimeout = 65536;
                client.ReceiveTimeout = 65536;
                client.SendBufferSize = 65536;
                client.ReceiveBufferSize = 65536;

                Console.WriteLine("#new tcp client connected. " + client.Client.RemoteEndPoint);
                ThreadPool.QueueUserWorkItem(HandleRequest, new TcpState(client));
                
            }
           
        }

        static void Main(string[] args)
        {
            ProxyDone += (sender, e) =>
            {
                if (sender != null)
                {
                    Console.WriteLine("#Close client");
                    var client = (TcpClient)sender;
                    client.Close();
                }
            };
            Thread th = new Thread(Listen);
            th.Start();
            Console.WriteLine("started proxy");
            Console.ReadKey();
        }
    }
}
