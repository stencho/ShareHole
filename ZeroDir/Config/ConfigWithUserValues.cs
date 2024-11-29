using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir.Configuration
{
    public class ConfigWithUserValues : Dictionary<string, Dictionary<string, ConfigValue>>
    {
        string config_location = "shares";
        string[] sections;

        public int share_count => this.Keys.Count;

        public ConfigFileIO config_file;

        public ConfigWithUserValues() {        
            config_file = new ConfigFileIO(config_location);
            LoadShares();
        }

        public ConfigWithUserValues(string config_location) {
            this.config_location = config_location;
            config_file = new ConfigFileIO(config_location);
            LoadShares();
        }

        void LoadShares() {
            sections = config_file.GetAllSections();
            var values = config_file.LoadValues();

            foreach (var section in sections) {
                this.Add(section, values[section]);
            }

            config_file.WriteAllValuesToConfig(values);
        }
    }
}
