using CamCapture.core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
        private HttpListener ? listener;
        private Task ? HandleRequest;
        //private byte[] buffer = new byte[1024];
        private string ? staticPath = null;

        private Dictionary<string, List<RouteAction>> routes = new Dictionary<string, List<RouteAction>>();

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="handleStatic">if true, the server will deliver static files if routes address files</param>
        public HTTPServer(string ? staticPath)
        {
            this.staticPath = staticPath;
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

        /// <summary>
        /// Find the best fitting action described by route needle in haystack
        /// The route with the longest possible match will be returned, thus
        /// /foo/bar will match before /foo or /
        /// </summary>
        /// <param name="needle"></param>
        /// <param name="haystack"></param>
        /// <returns></returns>
        private RouteAction ? findAction(string needle, List<RouteAction> haystack)
        {
            int matchCount = 0;
            RouteAction ? candidate = null;

            foreach (RouteAction action in haystack)
            {
                int rl = action.Route.Length;
                if (needle.StartsWith(action.Route) && rl > matchCount) 
                {
                    matchCount = rl;
                    candidate = action;
                }
            }

            return candidate;
        }

        private bool handleStaticFiles(string route, HttpListenerResponse response)
        {
            if (route == null || staticPath == null) return false;
            string path = Path.Combine(staticPath, route.Substring(1)); // strip leading "/"

            // if route is / we try to redirect to index.html
            if (path == staticPath) path = Path.Combine(staticPath, "index.html");

            return sendFile(path, response);
        }

        public bool sendFile(string filename, HttpListenerResponse response)
        {
            if (string.IsNullOrEmpty(filename) || !File.Exists(filename)) return false;
            byte[] bytes = File.ReadAllBytes(filename);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.StatusCode = (int)HttpStatusCode.OK;
            response.Close();
            return true;
        }


        private async Task OnHandleRequest()
        {
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest request = ctx.Request;
                HttpListenerResponse response = ctx.Response;

                string method = request.HttpMethod;
                string path = request.Url?.AbsolutePath ?? "/";
                bool responded = (method == HttpMethod.Get.ToString()) && handleStaticFiles(path, response);

                if (!responded && routes.ContainsKey(method))
                {
                    RouteAction ? action = findAction(path, routes[method]);
                    if (action != null) responded = action.Invoke(request, response);
                }

                if (!responded)
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                }
            }
        }

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
            listener?.Close();
            HandleRequest = null;
            listener = null;
        }
    }
}
