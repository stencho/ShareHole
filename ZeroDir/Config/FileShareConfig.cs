﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir.Config
{
    public class FileShareConfig : Dictionary<string, Dictionary<string, ConfigValue>>
    {
        string config_location = "shares";
        string[] sections;

        public int share_count = 0;

        public ConfigFileIO config_file;

        public FileShareConfig() {        
            config_file = new ConfigFileIO(config_location);
            LoadShares();
        }

        public FileShareConfig(string config_location) {
            this.config_location = config_location;
            config_file = new ConfigFileIO(config_location);
            LoadShares();
        }

        void LoadShares() {
            sections = config_file.GetAllSections();
            var values = config_file.ToDictionary();

            foreach (var section in sections) {
                this.Add(section, values[section]);
                if (!this[section].ContainsKey("path")) {
                    Logging.ErrorAndThrow($"Share \"{section}\" doesn't contain a 'path' variable");
                    values.Remove(section);
                    continue;
                } 
            }

            config_file.WriteAllValuesToConfig(values);

            share_count = sections.Length;
        }
    }
}
