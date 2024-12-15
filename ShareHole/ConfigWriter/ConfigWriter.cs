using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole.Configuration
{
    public enum ValueType { STRING, INT, BOOL, IP, COLOR }

    public  class ConfigFileIO
    {
        internal List<string> config_file_text;
        internal string file_path;

        public ConfigFileIO(string file_path) {
            this.file_path = file_path;
            config_file_text = new List<string>();

            var fso = new FileStreamOptions();
            fso.Access = FileAccess.ReadWrite;
            fso.Mode = FileMode.OpenOrCreate;

            using (StreamReader sr = new StreamReader(file_path, fso)) {}

            try {
                config_file_text = File.ReadAllLines(file_path, Encoding.UTF8).ToList();
            } catch (FileNotFoundException ex) {
                Logging.Error(ex.Message);
            } catch (IOException ex) {
                Logging.Error(ex.Message);
            }

        }

        public void ChangeValueByString(Dictionary<string, Dictionary<string, ConfigValue>> values, string input) {
            string sec, key, val = "";
            int dot = input.IndexOf('.');
            int eq = input.IndexOf('=');
            if (dot > eq || eq == -1 || dot == -1) { Logging.Error($"Failed while attempting to write \"{input}\". Make sure it is in format \"section.key=value\"."); return; }
            else {
                sec = input.Substring(0, dot).Trim();
                key = input.Substring(dot+1, eq - (dot + 1)).Trim();
                val = input.Substring(eq + 1).Trim();

                if (values.ContainsKey(sec) && values[sec].ContainsKey(key)) {
                    Logging.Message($"Changing {sec}.{key} to \"{val}\" (was \"{values[sec][key].ToString()}\")");

                    switch (values[sec][key].value_type) {
                        case ValueType.STRING:
                            values[sec][key].set_string(val);
                            break;
                        case ValueType.INT:
                            int vi = 0; string_to_int(values, sec, key, out vi);
                            values[sec][key].set_int(vi);
                            break;
                        case ValueType.BOOL:
                            bool vb = false; string_to_bool(values, sec, key, out vb);
                            values[sec][key].set_bool(vb);
                            break;
                        case ValueType.IP:
                            byte[] vip = null; string_to_ip(values, sec, key, out vip);
                            values[sec][key].set_ip(vip);
                            break;
                        case ValueType.COLOR:
                            byte[] vc = null; string_to_color(values, sec, key, out vc);
                            values[sec][key].set_color(vc);
                            break;
                    }
                    //values[sec][key].SetValue(val);
                    Write(sec, key, val);                    
                }
            }
        }

        bool string_to_int(Dictionary<string, Dictionary<string, ConfigValue>> values, string section, string key, out int value) {
            string s = Read(section, key);
            int res;

            if (int.TryParse(s, out res)) {
                value = res;
                return true;
            }

            value = 0; 
            return false;
        }

        bool string_to_bool(Dictionary<string, Dictionary<string, ConfigValue>> values, string section, string key, out bool value) {
            string s = Read(section, key);
            bool res;

            if (bool.TryParse(s, out res)) {
                value = res;
                return true;
            }

            value = false;
            return false;
        }

        bool string_to_ip(Dictionary<string, Dictionary<string, ConfigValue>> values, string section, string key, out byte[] value) {
            string s = Read(section, key);
            string[] split = s.Split('.');
            value = new byte[4];

            if (split.Length != 4) goto error;

            for (int i = 0; i < split.Length; i++) {
                int res;

                if (int.TryParse(split[i], out res) && res >= 0 && res <= 255) {
                    value[i] = (byte)res;
                } else goto error;
            }

            return true;

        error:
            value = null;
            return false;
        }

        bool string_to_color(Dictionary<string, Dictionary<string, ConfigValue>> values, string section, string key, out byte[] value) {
            string s = Read(section, key);
            string[] split = s.Split(',');
            value = new byte[4];

            if (split.Length != 4) goto error;

            for (int i = 0; i < split.Length; i++) {
                int res;

                if (int.TryParse(split[i], out res) && res >= 0 && res <= 255) {
                    value[i] = (byte)res;
                } else goto error;
            }

            return true;

        error:
            value = null;
            return false;
        }

        public void LoadExpectedValues(ref Dictionary<string, Dictionary<string, ConfigValue>> defaults) {
            foreach (string section in defaults.Keys) {
                Logging.Config($"[{section}]");
                foreach (string key in defaults[section].Keys) {
                    if (KeyExists(section, key)) {
                        switch (defaults[section][key].value_type) {
                            case ValueType.STRING:
                                defaults[section][key].set_string(load_string_from_ini(section, key));
                                break;
                            case ValueType.INT:
                                defaults[section][key].set_int(load_int_from_ini_with_defaults(defaults, section, key));
                                break;
                            case ValueType.BOOL:
                                defaults[section][key].set_bool(load_bool_from_ini_with_defaults(defaults, section, key));
                                break;
                            case ValueType.IP:
                                defaults[section][key].set_ip(load_ip_from_ini_with_defaults(defaults, section, key));
                                break;
                            case ValueType.COLOR:
                                defaults[section][key].set_color(load_color_from_ini_with_defaults(defaults, section, key));
                                break;
                        }
                        Logging.Config($"| [{defaults[section][key].value_type.ToString()}] {section}.{key} = {defaults[section][key].ToString()}");
                    } else {
                        Write(section, key, defaults[section][key].ToString());
                        Logging.Config($"| [{defaults[section][key].value_type.ToString()}] {section}.{key} = {defaults[section][key].ToString()}");
                    }
                }
            }
            //write back to unfuck any malformed options
            WriteAllValuesToConfig(defaults);
            Clean(defaults);

            Flush();
        }

        string load_string_from_ini(string section, string name) {
            return Read(section, name);
        }

        public void WriteAllValuesToConfig(Dictionary<string, Dictionary<string, ConfigValue>> options) {
            foreach (string section in options.Keys) {
                foreach (var key in options[section].Keys) {
                    Write(section, key, options[section][key].ToString());
                }
            }
        }

        int load_int_from_ini_with_defaults(Dictionary<string, Dictionary<string, ConfigValue>> options, string section, string name) {
            string s = Read(section, name);
            int res;

            if (int.TryParse(s, out res)) {
                return res;
            }

            Logging.Warning($"Malformed integer value in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_int();
        }

        bool load_bool_from_ini_with_defaults(Dictionary<string, Dictionary<string, ConfigValue>> options, string section, string name) {
            string s = Read(section, name);

            if (s.ToUpper() == "TRUE") return true;
            else if (s.ToUpper() == "FALSE") return false;

            Logging.Warning($"Malformed boolean value in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_bool();
        }

        byte[] load_ip_from_ini_with_defaults(Dictionary<string, Dictionary<string, ConfigValue>> options, string section, string name) {
            string s = Read(section, name);
            string[] split = s.Split('.');
            byte[] ip = new byte[4];

            if (split.Length != 4) goto error;

            for (int i = 0; i < split.Length; i++) {
                int res;

                if (int.TryParse(split[i], out res) && res >= 0 && res <= 255) {
                    ip[i] = (byte)res;
                } else goto error;
            }

            return ip;

        error:
            Logging.Warning($"Malformed IP address in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_ip();
        }

        byte[] load_color_from_ini_with_defaults(Dictionary<string, Dictionary<string, ConfigValue>> options, string section, string name) {
            string s = Read(section, name);
            string[] split = s.Split(',');
            byte[] rgba = new byte[4];

            if (split.Length != 4) goto error;

            for (int i = 0; i < split.Length; i++) {
                int res;

                if (int.TryParse(split[i], out res) && res >= 0 && res <= 255) {
                    rgba[i] = (byte)res;

                } else goto error;
            }

            return rgba;

        error:
            Logging.Warning($"Malformed color in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_color();
        }

        public bool KeyExists(string section, string key) {
            bool in_correct_section = false;

            for (int i = 0; i < config_file_text.Count; i++) {
                string line = config_file_text[i].Trim();
                if (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) == section) in_correct_section = true;
                if (in_correct_section && line.StartsWith(key))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) return true;
                    else throw new Exception("Value not found!");
                }
                if (in_correct_section && line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) != section)
                    in_correct_section = false;
            }
            return false;
        }

        public bool SectionExists(string section) {
            for (int i = 0; i < config_file_text.Count; i++) {
                string line = config_file_text[i].Trim();
                if (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) == section) return true;
            }
            return false;
        }

        public string[] GetAllSections() {
            List<string> sections = new List<string>();
            for (int i = 0; i < config_file_text.Count; i++) {
                string line = config_file_text[i].Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) sections.Add(line.Substring(1, line.Length - 2));
            }
            return sections.ToArray();
        }

        public Dictionary <string, Dictionary<string, ConfigValue>> LoadValues() {
            Dictionary<string, Dictionary<string, ConfigValue>> dict = new Dictionary<string, Dictionary<string, ConfigValue>>();
            
            string current_section = "";

            for (int i = 0; i < config_file_text.Count; i++) {
                string line = config_file_text[i].Trim();

                //Section header
                if (line.StartsWith("[") && line.EndsWith("]")) {
                    current_section = line.Substring(1, line.Length - 2);
                    if (!dict.ContainsKey(current_section)) {
                        dict.Add(current_section, new Dictionary<string, ConfigValue>());
                        Logging.Config($"[{current_section}]");
                    }

                //checking for a key
                } else {
                    string key = "";
                    string value = "";
                    int eq = line.IndexOf('=');
                    if (eq == -1) continue;
                    else {
                        key = line.Substring(0, eq);
                        value = line.Substring(eq + 1);
                    }

                   // Logging.Message(key + " [=] " + value);

                    if (current_section.Length > 0) {
                        if (!dict[current_section].ContainsKey(key)) {
                            int ip = 0;
                            bool bp = false;
                            if (int.TryParse(value, out ip)) {
                                Logging.Config($"| [INT] {current_section}.{key} = {value}");
                                dict[current_section].Add(key, new ConfigValue(ip));          
                                
                            } else if (bool.TryParse(value, out bp)) {                                
                                Logging.Config($"| [BOOL] {current_section}.{key} = {value}");
                                dict[current_section].Add(key, new ConfigValue(bp));

                            } else if (value.Count(x => x == '.') ==  3) {
                                Logging.Config($"| [IP] {current_section}.{key} = {value}");
                                var str = value.Split(',');
                                byte[] byte_out = new byte[4];
                                for (int c = 0; c < str.Length; c++) {
                                    byte b;
                                    if (!byte.TryParse(str[c], out b)) {
                                        continue;
                                    }
                                    byte_out[c] = b;
                                }

                                dict[current_section].Add(key, new ConfigValue(byte_out));

                            } else if (value.Count(x => x == ',') == 3) {
                                Logging.Config($"| [COLOR] {current_section}.{key} = {value}");

                                var str = value.Split(',');
                                byte[] col = new byte[4];
                                for (int c = 0; c < str.Length; c++) {
                                    byte b;
                                    if (!byte.TryParse(str[c], out b)) {
                                        continue;
                                    }
                                    col[c] = b;
                                }
                               
                                dict[current_section].Add(key, new ConfigValue(Color.FromArgb(col[3], col[0], col[1], col[2])));
                            } else {
                                Logging.Config($"| [STRING] {current_section}.{key} = {value}");
                                dict[current_section].Add(key, new ConfigValue(value));
                            }

                        }
                    }
                }
            }

            return dict;
        }

        public string Read(string section, string key) {
            bool in_correct_section = false;

            for (int i = 0; i < config_file_text.Count; i++) {
                string line = config_file_text[i].Trim();

                if (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) == section) in_correct_section = true;
                if (in_correct_section && line.StartsWith(key)) {
                    int eq = line.IndexOf('=') + 1;
                    if (eq > 0) return line.Substring(eq, line.Length - eq);
                    else throw new Exception("Value not found!");
                }

                if (in_correct_section && line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) != section)
                    in_correct_section = false;
            }

            throw new Exception("Value not found!");
        }

        public void Write(string section, string key, string value) {
            bool in_correct_section = false;
            bool loop_2 = false;

            for (int i = 0; i < config_file_text.Count; i++) {
                string line = config_file_text[i].Trim();
                //found a section with the right header
                if (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) == section) in_correct_section = true;
                //found the right key, replace its text
                if (in_correct_section && !loop_2 && line.StartsWith(key)) { config_file_text[i] = $"{key}={value}"; return; }
                //we're looping through a second time, so that if we didn't find the value the first time, we can write it at the bottom of the first correct section
                if (in_correct_section && loop_2 && (i == config_file_text.Count - 1 || line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) != section))
                {
                    config_file_text.Insert(i + 1, $"{key}={value}");
                    return;
                }
                //we're leaving the correct section on the first loop, continue and see if it shows up again later
                if (in_correct_section && line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) != section)
                {
                    in_correct_section = false;
                }
                if (i == config_file_text.Count - 1 && loop_2 == false) { i = 0; loop_2 = true; in_correct_section = false; }
            }
        }

        public void CreateSectionIfNotExists(string section) {
            for (int i = 0; i < config_file_text.Count; i++) {
                string line = config_file_text[i].Trim();
                //found a section with the right header
                if (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) == section) break;
            }
            config_file_text.Add($"[{section}]");
        }

        public void Flush() {
            File.WriteAllText(file_path, string.Join('\n', config_file_text));
        }

        public void Clean(Dictionary<string, Dictionary<string, ConfigValue>> values) {
            config_file_text.Clear();
            foreach (string section in values.Keys) {
                config_file_text.Add($"[{section}]");
                foreach (var key in values[section].Keys) {
                    config_file_text.Add($"{key}={values[section][key]}");
                }
                config_file_text.Add("");
            }

        }
    }


    public class ConfigValue {
        public readonly ValueType value_type;

        internal string name = "";

        object value;
        object default_value;
        
        public ConfigValue(string default_value) {
            value_type = ValueType.STRING;
            set_string(default_value);
            this.default_value = default_value;
        }
        public ConfigValue(int default_value) {
            value_type = ValueType.INT;
            set_int(default_value);
            this.default_value = default_value;
        }
        public ConfigValue(bool default_value) {
            value_type = ValueType.BOOL;
            set_bool(default_value);
            this.default_value = default_value;
        }
        public ConfigValue(byte[] default_value) {
            value_type = ValueType.IP;
            set_ip(default_value);

            if (default_value.Length == 4) this.default_value = default_value;
            else throw new Exception("Wrong number of bytes in IP!");
        }
        public ConfigValue(Color default_value) {
            value_type = ValueType.COLOR;

            set_color(new byte[] { default_value.R, default_value.G, default_value.B, default_value.A });
            this.default_value = default_value;
        }

        public bool IsValid<T> (T value) {
            var t = typeof(T);

                 if (t == typeof(string) && value_type == ValueType.STRING) return true;
            else if (t == typeof(int) && value_type == ValueType.INT) return true;
            else if (t == typeof(bool) && value_type == ValueType.BOOL) return true;
            else if (t == typeof(byte[]) && value_type == ValueType.IP) return true;
            else if (t == typeof(Color) && value_type == ValueType.COLOR) return true;
            
            return false;
        }

        public void SetValue<T>(T value) where T : notnull {
            var as_string = "";
            var t = typeof(T);
            if (t == typeof(string) && value_type == ValueType.STRING) {
                as_string = Convert.ToString(value);
                set_string(as_string);
            } else if (t == typeof(int) && value_type == ValueType.INT) {
                var i = Convert.ToInt32(value);
                set_int(i);
                as_string = i.ToString();
            } else if (t == typeof(bool) && value_type == ValueType.BOOL) {
                var b = Convert.ToBoolean(value);
                set_bool(b);
                as_string = b.ToString().ToLower();
            } else if (t == typeof(byte[]) && value_type == ValueType.IP) {
                set_ip(value as byte[]);
                as_string = string.Join('.', value as byte[]);
            } else if (t == typeof(Color) && value_type == ValueType.COLOR) {
                set_color(value as byte[]);
                as_string = string.Join(',', value as byte[]);
            } else {
                Logging.ErrorAndThrow($"{t.ToString()} is an invalid type! Expected {value_type}");
            }

            Logging.Message($"Set value of \"{name}\" to \"{as_string}\"");
        }


        // SET VALUES
        public void set_string(string value) {
            if (value_type == ValueType.STRING) this.value = value;
            else throw new TypeAccessException();
        }
        public void set_int(int value) {
            if (value_type == ValueType.INT) this.value = value;
            else throw new TypeAccessException();
        }

        public void set_bool(bool value) {
            if (value_type == ValueType.BOOL) this.value = value;
            else throw new TypeAccessException();
        }

        public void set_ip(byte[] value) {
            if (value_type == ValueType.IP) {
                if (value.Length == 4) this.value = value;
                else throw new Exception("Wrong number of bytes in IP!");
            }
            else throw new TypeAccessException();
        }
        public void set_color(byte[] value) {
            if (value_type == ValueType.COLOR) {
                if (value.Length == 4) this.value = value;
                else throw new Exception("Wrong number of bytes in color!");
            }
            else throw new TypeAccessException();
        }

        // GET VALUES
        public string get_string() {
            if (value_type == ValueType.STRING) return (string)value;
            else throw new TypeAccessException();
        }

        public int get_int() {
            if (value_type == ValueType.INT) return (int)value;
            else throw new TypeAccessException();
        }

        public bool get_bool() {
            if (value_type == ValueType.BOOL) return (bool)value;
            else throw new TypeAccessException();
        }

        public byte[] get_ip() {
            if (value_type == ValueType.IP) return (byte[])value;
            else throw new TypeAccessException();
        }
        public Color get_color() {
            if (value_type == ValueType.COLOR) return (Color)value;
            else throw new TypeAccessException();
        }

        // GET DEFAULTS
        public string get_default_string() {
            if (value_type == ValueType.STRING) return (string)default_value;
            else throw new TypeAccessException();
        }

        public int get_default_int() {
            if (value_type == ValueType.INT) return (int)default_value;
            else throw new TypeAccessException();
        }
        public bool get_default_bool() {
            if (value_type == ValueType.BOOL) return (bool)default_value;
            else throw new TypeAccessException();
        }

        public byte[] get_default_ip() {
            if (value_type == ValueType.IP) return (byte[])default_value;
            else throw new TypeAccessException();
        }
        public byte[] get_default_color() {
            if (value_type == ValueType.COLOR) return (byte[])default_value;
            else throw new TypeAccessException();
        }

        // OTHER
        public override string ToString() {
            switch (value_type) {
                case ValueType.STRING: return (string)value;
                case ValueType.INT: return ((int)value).ToString();
                case ValueType.BOOL: return ((bool)value).ToString().ToLower();
                case ValueType.IP: return string.Join('.', (byte[])value);
                case ValueType.COLOR: return string.Join(',', (byte[])value);
                default: throw new Exception();
            }
        }
    }
}