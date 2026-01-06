using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CamCapture
{
    class TCPServer : Server
    {
        private TcpListener listener;
        private Thread listenerThread;
        private bool started;
        private struct UserData
        {
            public Socket socket;
            public byte[] data;
        }

        public TCPServer() { 
            started = false;
        }

        private void acceptConnection()
        {
            listenerThread = new Thread(() =>
            {
                while (listenerThread.IsAlive)
                {
                    try
                    {
                        Socket s = listener.AcceptSocket();
                        Console.WriteLine($"accept {s.ToString()}");
                        string mesg = "Hello World";
                        s.Send( Encoding.UTF8.GetBytes(mesg) );

                        UserData data = new UserData
                        {
                            socket = s,
                            data = new byte[1024]
                        };

                        s.BeginReceive(data.data, 0, 1024, SocketFlags.None, OnReceive, data);
                    }
                    catch
                    {
                        break;
                    }

                }
            });
            listenerThread.Start();
        }
        private void OnReceive(System.IAsyncResult res)
        {
            UserData data = (UserData)res.AsyncState;
            Socket s = data.socket;
            int read = s.EndReceive(res);

            string json = Encoding.UTF8.GetString(data.data, 0, read);
            try
            {
                Dictionary<string, string> map = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                string response = Request(map); // nothing to do here currently
            }
            catch {
            }

            s.BeginReceive(data.data,0,1024, SocketFlags.None,OnReceive, data);

        }

        public override bool IsConnected
        {
            get => started;
        }

        public override void Start(int port)
        {
            Stop();
            listener = new TcpListener(port);
            try
            { 
                listener.Start();
                acceptConnection();
                started = true;
            }
            catch
            {
                Stop();
            }
        }

        public override void Stop()
        {
            started = false;
            listener?.Stop();
            listener = null;
            listenerThread?.Interrupt();
            listenerThread?.Join();
            listenerThread = null;
        }
    }
}
