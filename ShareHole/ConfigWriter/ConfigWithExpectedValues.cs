using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole.Configuration {
    public class ConfigWithExpectedValues : Dictionary<string, Dictionary<string, ConfigValue>> {
        string config_location = "server";
        string config_full_path => new FileInfo(config_location).FullName;

        public ConfigFileIO config_file;

        public ConfigWithExpectedValues(Dictionary<string, Dictionary<string, ConfigValue>> expected_values) : base() {
            foreach (string section in expected_values.Keys)
                this.Add(section, expected_values[section]);
            
            config_file = new ConfigFileIO(config_full_path);
            config_file.LoadExpectedValues(ref expected_values);
        }

        ~ConfigWithExpectedValues() {
            Clean();
        }

        public void SetValueAndWrite<T>(string section, string key, T value) where T : notnull {
            this[section][key].SetValue(value);
            write_value(section, key);
        }

        void write_value(string section, string key) {
            if (!ContainsKey(key)) Logging.ErrorAndThrow($"Key {key} is not a valid configuration option!");
            config_file.Write(section, key, this[section][key].ToString());
        }
                
        public void Clean() {
            config_file.WriteAllValuesToConfig(this);
            config_file.Clean(this);
            config_file.Flush();
        }
    }


}
