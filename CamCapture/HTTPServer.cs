using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace CamCapture
{
    class HTTPServer : Server
    {
        private HttpListener listener;
        private Task HandleRequest;
        private byte[] buffer = new byte[1024];




        public override bool IsConnected
        {
            get => listener != null;
        }

        private async Task OnHandleRequest()
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest request = ctx.Request;
                HttpListenerResponse response = ctx.Response;

                bool ishandled = false;
                if (request.HttpMethod == "POST") ishandled = onPostRequest(request, response);
                
                if (!ishandled)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Close();
                }

            }
        }

        private bool onPostRequest(HttpListenerRequest req, HttpListenerResponse res)
        {
            string uri = req.Url.AbsolutePath;

            if (uri != "/screenshot") return false;

            Dictionary<string, string> map = null;

            if (req.HasEntityBody)
            {
                int read = req.InputStream.Read(buffer, 0, 1024);
                if (read > 0)
                {
                    string str = Encoding.UTF8.GetString(buffer, 0, read);
                    try
                    { 
                        map = JsonConvert.DeserializeObject<Dictionary<string, string>>(str);
                    } catch
                    {

                    }
                }
            }
            if (map == null) map = new Dictionary<string, string>();
            map["screenshot"] = "true";
            string answer = Request(map);

            if (answer != null)
            {
                res.AddHeader("Content-Type", "application/json");
                res.OutputStream.Write(Encoding.UTF8.GetBytes(answer));
            }

            res.StatusCode = (int)HttpStatusCode.OK;
            res.Close();
            return true;
        }




        public override void Start(int port)
        {
            listener = new HttpListener();
            // Attention: pathes have to end with /
            listener.Prefixes.Add($"http://*:{port}/");
            try
            {
                listener.Start();
                HandleRequest = OnHandleRequest();
            }
            catch
            {
                Stop();
            }
        }

        public override void Stop()
        {
            listener.Close();
            HandleRequest = null;
            listener = null;
        }
    }
}
