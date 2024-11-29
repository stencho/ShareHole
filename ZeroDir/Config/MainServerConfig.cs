using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir.Config {
    public class ServerConfig {
        string config_location = "server";
        string config_full_path => new FileInfo(config_location).FullName;

        public ConfigFileIO config_file;

        public Dictionary<string, Dictionary<string, ConfigValue>> values = new Dictionary<string, Dictionary<string, ConfigValue>>() {
            { "server",
                new Dictionary<string, ConfigValue>() {
                    { "prefix", new ConfigValue("localhost") },
                    { "port", new ConfigValue(8080) },
                    { "threads", new ConfigValue(32) },
                    { "passdir", new ConfigValue("loot") }
                }
            }
            };


        public ServerConfig(string config_path) {
            config_location = config_path;
            config_file = new ConfigFileIO(config_full_path);
            values = config_file.LoadFromIniIntoNestedDictWithDefaults(values);
        }

        ~ServerConfig() {
            Clean();
        }

        public void SetValueAndWrite<T>(string section, string key, T value) where T : notnull {
            values[section][key].SetValue(value);
            write_value(section, key);
        }

        void write_value(string section, string key) {
            if (!values.ContainsKey(key)) Logging.ErrorAndThrow($"Key {key} is not a valid configuration option!");
            config_file.Write(section, key, values[section][key].ToString());
        }
                
        public void Clean() {
            config_file.WriteAllValuesToConfig(values);
            config_file.Clean(values);
            config_file.Flush();
        }
    }


}
