using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CamCapture.core
{
    /// <summary>
    /// Implements a REST Interface specific for the app utilizing the HTTPServer
    /// </summary>
    internal class REST
    {
        /// <summary>
        /// Function Specification that will be called on Capture Requests
        /// </summary>
        /// <param name="prefix">optional prefix where capture should be stored</param>
        /// <returns>filename of image that was captured</returns>
        public delegate string ? TCaptureFunc(string? prefix);
        /// <param name="name">name of the file</param>
        /// <returns>full path of file if exists or null</returns>
        public delegate string ? TImageFunc(string name);
        public delegate string[] TQueryFunc(string? prefix, string ? from, string ? to);
        private TImageFunc ? imageFunc;
        private TCaptureFunc ? captureFunc;
        private TQueryFunc ? queryFunc;
        private HTTPServer server;
        private byte[] readBuffer = new byte[1024 * 1024];

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="server">Server to use</param>
        public REST(HTTPServer server)
        {
            this.server = server;
            server.AddRouteAction(HttpMethod.Post, new RouteAction("/capture", onPostCapture ));
            server.AddRouteAction(HttpMethod.Get, new RouteAction("/capture", onGetCapture ));
            server.AddRouteAction(HttpMethod.Get, new RouteAction("/list", onGetList));
            server.AddRouteAction(HttpMethod.Get, new RouteAction("/image", onGetImage));
        }

        public TImageFunc ImageFunc { set => imageFunc = value; }

        /// <summary>
        /// Function which will be called on Capture Requests
        /// </summary>
        public TCaptureFunc CaptureFunc { set => captureFunc = value; }

        /// <summary>
        /// Function that will be called to get information about already stored 
        /// captured files
        /// </summary>
        public TQueryFunc QueryFunc { set => queryFunc = value; }


        private bool onGetImage(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (imageFunc == null) return false;

            string[] parts = request.Url?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
            if (parts.Length < 2 ) return false;
            string? path = imageFunc(parts[1]);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                response.StatusCode = (int)HttpStatusCode.NotFound;
            else
                server.sendFile(path, response); // return value ignored because handled differently here

            response.Close();
            return true;
        }

        /// <summary>
        /// Route for all captured files route maybe /prefix/from/till
        /// From and To must be dates, prefix is a string
        /// from hasto preceed till. Prefix can not be a number
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private bool onGetList(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (queryFunc == null) return false;

            string[] parts = request.Url?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? new string[1] { "/" };

            // All numbers in the order - Skip 1 because of route
            List<string> numbers = parts.Skip(1).Where((s)=>Regex.IsMatch(s, "^[0-9]+$")).ToList();
            // All prefix candidates
            List<string> remaining = parts.Skip(1).Where((s)=>!numbers.Contains(s)).ToList();

            string? prefix = remaining.FirstOrDefault();
            string? from = numbers.FirstOrDefault();
            if (numbers.Count() > 0) numbers.RemoveAt(0);
            string? to = numbers.FirstOrDefault();

            string[] res = queryFunc(prefix, from, to);
            sendJson(response, res);
            return true;
        }



        private bool onPostCapture(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (captureFunc == null) return false;
            string? prefix = null;
            if ( (request.HttpMethod == HttpMethod.Post.ToString()) && request.HasEntityBody)
            {
                string? bodystr = getBody(request);
                if (bodystr != null)
                {
                    try
                    {
                        Dictionary<string, string> ? body = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodystr);
                        if (body != null && body.ContainsKey("prefix")) prefix = body["prefix"];
                    } catch { }
                }
            }

            string? file = captureFunc(prefix);
            if (file == null) return false;
            sendJson(response, System.IO.Path.GetFileName(file));
            return true;
        }

        private bool onGetCapture(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (captureFunc == null) return false;
            string ? prefix = null;

            if (request.HttpMethod == HttpMethod.Get.ToString())
            {
                string[] parts = request.Url?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries ) ?? new string[0];
                if (parts.Length > 1) prefix = parts[1];
            }

            string? file = captureFunc(prefix);
            if (file == null) return false;
            sendJson(response, System.IO.Path.GetFileName(file));
            return true;
        }

        /// <summary>
        /// Sends the given Data as json. The Object will be Serialized using JsonConvert.SerializeObject
        /// </summary>
        /// <param name="res">Http Response</param>
        /// <param name="o">Object to send</param>
        private void sendJson(HttpListenerResponse res, object o)
        {
            res.AddHeader("Content-Type", "application/json");
            string str = JsonConvert.SerializeObject(o, Formatting.Indented);
            res.OutputStream.Write(Encoding.UTF8.GetBytes(str));
            res.StatusCode = (int)HttpStatusCode.OK;
            res.Close();
        }

        /// <summary>
        /// Retreives the body data from Post data and returns it as String
        /// </summary>
        /// <param name="req">Http Request</param>
        /// <returns>body data as String (most likely json)</returns>
        private string ? getBody(HttpListenerRequest req)
        {
            if (!req.HasEntityBody) return null;
            int read = req.InputStream.Read(readBuffer, 0, readBuffer.Length);
            string res = Encoding.UTF8.GetString(readBuffer, 0, read);
            return res;
        }

    }
}
