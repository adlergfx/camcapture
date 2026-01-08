using CamCapture.core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Windows.Media.Media3D;

namespace CamCapture
{
    class HTTPServer 
    {
        private HttpListener listener;
        private Task HandleRequest;
        private byte[] buffer = new byte[1024];

        private Dictionary<string, List<RouteAction>> routes = new Dictionary<string, List<RouteAction>>();
        private byte[] readBuffer = new byte[1024 * 1024];
        private byte[] jsonBuffer = new byte[1024 * 1024];

        public HTTPServer()
        {
            AddRouteAction(HttpMethod.Get, new RouteAction("/screenshot", TestAction));
        }

        public bool IsConnected
        {
            get => listener != null;
        }

        public void AddRouteAction(HttpMethod method, RouteAction action)
        {
            string mstr = method.ToString();
            if (!routes.ContainsKey(mstr))
            {
                routes[mstr] = new List<RouteAction>();
            }
            routes[mstr].Add(action);
        }

        private bool TestAction(HttpListenerRequest request, HttpListenerResponse response)
        {
            Console.WriteLine("finally");
            response.StatusCode = (int)HttpStatusCode.OK;
            response.Close();
            return true;
        }

        public string getBody(HttpListenerRequest req)
        {
            if (!req.HasEntityBody) return null;
            int read = req.InputStream.Read(readBuffer, 0, readBuffer.Length);
            string res = Encoding.UTF8.GetString(readBuffer, 0, read);
            return res;
        }

        public void sendJson(HttpListenerResponse res, object o)
        {
            res.AddHeader("Content-Type", "application/json");
            string str = JsonConvert.SerializeObject(o, Formatting.Indented);
            res.OutputStream.Write(Encoding.UTF8.GetBytes(str));
            res.StatusCode = (int)HttpStatusCode.OK;
            res.Close();
        }

        private async Task OnHandleRequest()
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest request = ctx.Request;
                HttpListenerResponse response = ctx.Response;

                string method = request.HttpMethod;
                string path = request.Url.AbsolutePath;
                bool responded = false;

                if (routes.ContainsKey(method))
                {
                    foreach (RouteAction action in routes[method])
                    {
                        if (action.matches(path))
                        {
                            responded = action.Invoke(request, response);
                            if (responded) break;
                        }
                    }
                }

                if (!responded)
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                }



                /*


                bool ishandled = false;
                if (request.HttpMethod == "POST") ishandled = onPostRequest(request, response);
                
                if (!ishandled)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Close();
                }
                */
            }
        }

        /*
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
        }*/




        public void Start(int port)
        {
            listener = new HttpListener();
            // Attention: pathes have to end with /
            listener.Prefixes.Add($"http://*:{port}/");
            try
            {
                listener.Start();
                HandleRequest = OnHandleRequest();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Stop();
            }
        }

        public void Stop()
        {
            listener.Close();
            HandleRequest = null;
            listener = null;
        }
    }
}
