// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.IO;
using System.Xml.Serialization;
using LibreLancer;
namespace LancerEdit
{
    public enum CameraModes
    {
        Default,
        Orbit
    }

    public class EditorConfiguration
    {
        public int MSAA;
        public int TextureFilter;
        public CameraModes CameraMode;
        public bool ViewButtons;

        public void Save()
        {
            using(var s = File.Create(configPath))
            {
                serializer.Serialize(s, this);
            }
        }

        public static EditorConfiguration Load()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    using (var s = File.OpenRead(configPath))
                    {
                        return (EditorConfiguration)serializer.Deserialize(s);
                    }
                }
                else
                    return new EditorConfiguration();
            }
            catch (Exception)
            {
                FLLog.Error("Config", "Error loading lanceredit.xml");
                return new EditorConfiguration();
            }
        }

        static XmlSerializer serializer = new XmlSerializer(typeof(EditorConfiguration));
        static string configPath;
        static EditorConfiguration()
        {
            SetConfigPath();
            FLLog.Info("Config", "Path: " + configPath);
        }

        static void SetConfigPath()
        {
            if (Platform.RunningOS == OS.Windows)
                configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lanceredit.xml");
            else
            {
                string osConfigDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (String.IsNullOrEmpty(osConfigDir))
                {
                    osConfigDir = Environment.GetEnvironmentVariable("HOME");
                    if (String.IsNullOrEmpty(osConfigDir))
                    {
                        configPath = "lanceredit.xml";
                        return;
                    }
                    osConfigDir += "/.config/";
                }
                configPath = Path.Combine(osConfigDir, "lanceredit.xml");
            }
        }
    }
}
