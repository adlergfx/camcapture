using DirectShowLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CamCapture.core
{
    // AI Generated with Claude 
    // 12.02.2026
    internal class DSCameraConfig
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

        private class CameraControlMap : Dictionary<string, PropertyValue> { }
        private class VideoProcAmpMap : Dictionary<string, PropertyValue> { }

        private class CameraSettings
        {
            public CameraControlMap? CameraControl { get; set; }
            public VideoProcAmpMap? VideoProcAmp { get; set; }
        }

        private class ConfigMap : Dictionary<string, CameraSettings> { }

        public static void ConfigCamera(DsDevice device, IBaseFilter sourceFilter)
        {
            ConfigMap records = new ConfigMap();
            if (File.Exists(filename))
            {
                string json = File.ReadAllText(filename, Encoding.UTF8);
                ConfigMap? rec = JsonConvert.DeserializeObject<ConfigMap>(json);
                if (rec != null) records = rec;
            }

            string name = device.Name;

            // Get interfaces
            IAMCameraControl? cameraControl = sourceFilter as IAMCameraControl;
            IAMVideoProcAmp? videoProcAmp = sourceFilter as IAMVideoProcAmp;

            if (records.ContainsKey(name))
            {
                // Load and apply existing settings
                CameraSettings settings = records[name];

                // Check if settings are empty (old format or corrupted)
                if (settings.CameraControl == null && settings.VideoProcAmp == null)
                {
                    // Remove old entry and recreate
                    records.Remove(name);
                }
                else
                {
                    // Apply CameraControl properties
                if (cameraControl != null && settings.CameraControl != null)
                {
                    foreach (string keyStr in settings.CameraControl.Keys)
                    {
                        PropertyValue value = settings.CameraControl[keyStr];
                        try
                        {
                            if (!Enum.TryParse<CameraControlProperty>(keyStr, out CameraControlProperty key))
                                continue;

                            CameraControlFlags ccf = ParseFlags<CameraControlFlags>(value.flags);
                            int hr = cameraControl.Set(key, value.value, ccf);
                            if (hr != 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set CameraControl {key} to {value.value}: HR=0x{hr:X8}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Exception setting CameraControl {keyStr}: {ex.Message}");
                            continue;
                        }
                    }
                }

                // Apply VideoProcAmp properties
                if (videoProcAmp != null && settings.VideoProcAmp != null)
                {
                    foreach (string keyStr in settings.VideoProcAmp.Keys)
                    {
                        PropertyValue value = settings.VideoProcAmp[keyStr];
                        try
                        {
                            if (!Enum.TryParse<VideoProcAmpProperty>(keyStr, out VideoProcAmpProperty key))
                                continue;

                            VideoProcAmpFlags vpaFlags = ParseFlags<VideoProcAmpFlags>(value.flags);
                            int hr = videoProcAmp.Set(key, value.value, vpaFlags);
                            if (hr != 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set VideoProcAmp {key} to {value.value}: HR=0x{hr:X8}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Exception setting VideoProcAmp {keyStr}: {ex.Message}");
                            continue;
                        }
                    }
                }

                    return; // Settings applied, exit
                }
            }

            // Create new settings from current camera state (no existing valid settings found)
            {
                CameraSettings settings = new CameraSettings();

                // Read CameraControl properties
                if (cameraControl != null)
                {
                    CameraControlMap ccMap = new CameraControlMap();
                    string[] pnames = Enum.GetNames(typeof(CameraControlProperty));

                    foreach (string pname in pnames)
                    {
                        CameraControlProperty p = (CameraControlProperty)Enum.Parse(typeof(CameraControlProperty), pname);
                        int val = 0;
                        CameraControlFlags flags;
                        int min, max, step, defaultVal;

                        try
                        {
                            int hr = cameraControl.GetRange(p, out min, out max, out step, out defaultVal, out flags);
                            if (hr != 0) continue;

                            hr = cameraControl.Get(p, out val, out flags);
                            if (hr != 0) continue;

                            ccMap[pname] = new PropertyValue(val, flags.ToString());
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (ccMap.Count > 0)
                        settings.CameraControl = ccMap;
                }

                // Read VideoProcAmp properties
                if (videoProcAmp != null)
                {
                    VideoProcAmpMap vpaMap = new VideoProcAmpMap();
                    string[] pnames = Enum.GetNames(typeof(VideoProcAmpProperty));

                    foreach (string pname in pnames)
                    {
                        VideoProcAmpProperty p = (VideoProcAmpProperty)Enum.Parse(typeof(VideoProcAmpProperty), pname);
                        int val = 0;
                        VideoProcAmpFlags flags;
                        int min, max, step, defaultVal;

                        try
                        {
                            int hr = videoProcAmp.GetRange(p, out min, out max, out step, out defaultVal, out flags);
                            if (hr != 0) continue;

                            hr = videoProcAmp.Get(p, out val, out flags);
                            if (hr != 0) continue;

                            vpaMap[pname] = new PropertyValue(val, flags.ToString());
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (vpaMap.Count > 0)
                        settings.VideoProcAmp = vpaMap;
                }

                if (settings.CameraControl != null || settings.VideoProcAmp != null)
                {
                    records[name] = settings;
                    string json = JsonConvert.SerializeObject(records, Formatting.Indented);
                    File.WriteAllText(filename, json);
                }
            }
        }

        private static T ParseFlags<T>(string flagsStr) where T : struct
        {
            if (flagsStr.Contains(","))
            {
                // Multiple flags combined (e.g. "Manual, Auto")
                string[] flagParts = flagsStr.Split(',');
                int combinedValue = 0;
                foreach (string flagPart in flagParts)
                {
                    string trimmed = flagPart.Trim();
                    if (Enum.TryParse<T>(trimmed, out T parsedFlag))
                    {
                        combinedValue |= Convert.ToInt32(parsedFlag);
                    }
                }
                return (T)Enum.ToObject(typeof(T), combinedValue);
            }
            else
            {
                // Single flag
                if (Enum.TryParse<T>(flagsStr, out T result))
                {
                    return result;
                }
                return default;
            }
        }
    }
}
