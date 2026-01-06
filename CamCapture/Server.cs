using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace CamCapture
{
    abstract class Server
    {
        public delegate string RequestHandler(Dictionary<string, string> req);
        public event RequestHandler OnRequest = delegate { return null; };


        public Server()
        {
        }

        protected string Request(Dictionary<string, string> map)
        {
            return OnRequest(map);
        }

        public abstract void Start(int port);
        public abstract void Stop();
        public abstract bool IsConnected
        {
            get;
        }

    }
}
