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
            string hostAndPort =
                arr.Where(str => str.StartsWith("HOST", StringComparison.OrdinalIgnoreCase)).ToArray()[0].Split(' ')[1];

            string host = host = hostAndPort.Split(':')[0];
            int port = hostAndPort.Contains(':') ? int.Parse(hostAndPort.Split(':')[1]) : 80;

            try
            {
                TcpClient remoteTcp = new TcpClient(host, port);
                if (remoteTcp.Connected) {
                    remoteTcp.SendBufferSize = 4096;
                    remoteTcp.SendTimeout = 4096;
                    remoteTcp.ReceiveTimeout = 8192;
                    remoteTcp.ReceiveBufferSize = 8192;

                    NetworkStream ns = remoteTcp.GetStream();
                    string strToSend = string.Join("\r\n", arr);
                    //Http header end with a space line;
                    strToSend += "\r\n\r\n";
                    byte[] buf = Encoding.ASCII.GetBytes(strToSend);
                    Console.WriteLine(strToSend);
                    ns.Write(buf, 0, buf.Length);
                    
                    Stream targetStream = client.GetStream();
                    byte[] recvbuf = new byte[4096];

                    int nRead = ns.Read(recvbuf, 0, recvbuf.Length);
                    while (nRead > 0) {
                        targetStream.Write(recvbuf, 0, nRead);
                        nRead = ns.Read(recvbuf, 0, recvbuf.Length);
                    }

                    if (ProxyDone != null) {
                        ProxyDone(client, new EventArgs());
                    }
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
                Console.WriteLine(result);
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
            ThreadPool.SetMaxThreads(16, 32);
            while (true)
            {
                //blocked, wait for a request
                TcpClient client = listener.AcceptTcpClient();
                client.SendTimeout = 8192;
                client.ReceiveTimeout = 2000;
                client.SendBufferSize = 8192;
                client.ReceiveBufferSize = 32768;

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
