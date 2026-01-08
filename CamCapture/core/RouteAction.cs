using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CamCapture.core
{
    internal class RouteAction
    {
        public delegate bool Handler(HttpListenerRequest request, HttpListenerResponse response);
        public event Handler handler = delegate { return false; };
        private string route;

        public RouteAction(string route, Handler ? action)
        {
            this.route = route;
            if (action != null) handler += action;
        }


        public bool matches(string uri)
        {
            return uri.StartsWith(this.route);
        }

        public bool Invoke(HttpListenerRequest request, HttpListenerResponse response)
        {
            return handler.Invoke(request, response);
        }

    }
}
