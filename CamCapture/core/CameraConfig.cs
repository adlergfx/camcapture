using AForge.Video.DirectShow;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CamCapture.core
{

    internal class CameraConfig
    {
        private static readonly string filename = "camconfig.json";

        private record PropertyValue
        {
            public PropertyValue(int v, string f)
            {
                value = v;
                flags = f;
            }

            public int value;
            public string flags;
        }

        private class PropertyMap: Dictionary<CameraControlProperty, PropertyValue> { }
        private class ConfigMap : Dictionary<string, PropertyMap > { }

        public static void ConfigCamera (FilterInfo caminfo, VideoCaptureDevice cam)
        {
            ConfigMap records = null;
            if (File.Exists(filename))
            {
                string json = File.ReadAllText(filename, Encoding.UTF8);
                records = JsonConvert.DeserializeObject<ConfigMap>(json);
            }
            else
            {
                records = new ConfigMap();
            }

            string name = caminfo.Name;

            if (records.ContainsKey(name))
            {
                PropertyMap map = records[name];
                foreach( CameraControlProperty key in map.Keys )
                {
                    PropertyValue value = map[key];
                    try
                    {
                        CameraControlFlags ccf = (CameraControlFlags)Enum.Parse(typeof(CameraControlFlags), value.flags);
                        cam.SetCameraProperty(key, value.value, ccf);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            else
            {
                PropertyMap map = new PropertyMap();
                string[] pnames =  Enum.GetNames(typeof(CameraControlProperty));
                foreach (string pname in pnames)
                {
                    CameraControlProperty p = (CameraControlProperty)Enum.Parse(typeof(CameraControlProperty), pname);
                    int val = 0;
                    CameraControlFlags f;
                    try
                    {
                        cam.GetCameraProperty(p, out val, out f);
                        string flag = f.ToString();
                    }
                    catch (NotSupportedException)
                    {
                        return;
                    }
                    map[p] = new PropertyValue(val, f.ToString());
                }
                records[name] = map;
                string json = JsonConvert.SerializeObject(records, Formatting.Indented);
                File.WriteAllText(filename, json);
            }
        }
    }
}
