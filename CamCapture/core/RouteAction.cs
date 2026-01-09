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
        private List<string> routeSegments;

        public RouteAction(string route, Handler ? action)
        {
            this.route = route;
            routeSegments = route.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (action != null) handler += action;
        }


        public string Route { get => route; }

        public List<string> RouteSegments { get => routeSegments; }



        public bool Invoke(HttpListenerRequest request, HttpListenerResponse response)
        {
            return handler.Invoke(request, response);
        }

    }
}
