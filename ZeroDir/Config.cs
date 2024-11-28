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

namespace ZeroDir {
    public enum ValueType { STRING, INT, BOOL, IP,
        COLOR
    }

    public class InstancedConfiguration {
        Configuration parent;
    }

    public class Configuration {
        string config_location = "config";
        string config_full_path => new FileInfo(config_location).FullName;

        internal class FileIO {            
            internal List<string> config_file_text;            
            internal string file_path;

            public FileIO(string file_path) {
                this.file_path = file_path;
                config_file_text = new List<string>();
                var fso = new FileStreamOptions();
                fso.Access = FileAccess.ReadWrite;
                fso.Mode = FileMode.OpenOrCreate;
                using (StreamReader sr = new StreamReader(file_path, fso)) {
                    string current_section = "";

                    while (sr.Peek() > -1) {
                        string line = sr.ReadLine();
                        if (line.StartsWith("[") && line.EndsWith("]")) { current_section = line.Substring(1, line.Length - 2); config_file_text.Add(line); }
                        else if (string.IsNullOrEmpty(current_section)) continue;
                        else if (line.Contains('=')) { config_file_text.Add(line); }                        
                    }
                }

                return;
                try {
                    config_file_text = File.ReadAllLines(file_path, Encoding.UTF8).ToList<string>();
                } catch (FileNotFoundException ex) {
                    Logging.Error(ex.Message);
                } catch (IOException ex) {
                    Logging.Error(ex.Message);
                }
            }

            public bool KeyExists(string section, string key) {
                bool in_correct_section = false;

                for (int i = 0; i < config_file_text.Count; i++) {
                    string line = config_file_text[i].Trim();
                    if (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) == section) in_correct_section = true;
                    if (in_correct_section && line.StartsWith(key)) {
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

            public string Read(string section, string key) {
                bool in_correct_section = false;

                for (int i = 0; i < config_file_text.Count; i++) {
                    string line = config_file_text[i].Trim();

                    if (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) == section) in_correct_section = true;
                    if (in_correct_section && line.StartsWith(key)) {
                        int eq = line.IndexOf('=')+1;
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
                    if (in_correct_section && loop_2 && ((i == config_file_text.Count - 1) || (line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) != section))) {                         
                        config_file_text.Insert(i+1, $"{key}={value}");
                        return;
                    }
                    //we're leaving the correct section on the first loop, continue and see if it shows up again later
                    if (in_correct_section && line.StartsWith("[") && line.EndsWith("]") && line.Substring(1, line.Length - 2) != section) {
                        in_correct_section = false;
                    }
                    if (i == config_file_text.Count-1 && loop_2 == false) { i = 0; loop_2 = true; in_correct_section = false; }
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
                File.WriteAllText(file_path, string.Join('\n',config_file_text));                
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

                Flush();
            }

        } 
        
        FileIO config_file;

        public Dictionary<string, Dictionary<string, ConfigValue>> options;

        public Configuration(string config_path, Dictionary<string, Dictionary<string, ConfigValue>> options) {
            this.config_location = config_path;
            this.options = options;
            config_file = new FileIO(config_full_path);
            LoadFromIni();

            Console.WriteLine(string.Join('\n', config_file.config_file_text));
        }

        ~Configuration() {
            config_file.Clean(options);
        }

        public void SetValueAndWrite<T>(string section, string key, T value) where T : notnull {
            options[section][key].SetValue(value);
            write_value(section, key);
        }

        void write_value(string section, string key) {
            if (!options.ContainsKey(key)) Logging.ErrorAndThrow($"Key {key} is not a valid configuration option!");
            config_file.Write(section, key, options[section][key].ToString());
        }

        public void write_all_values_to_disk() {
            foreach (string section in options.Keys) {
                foreach (var key in options[section].Keys) {
                    config_file.Write(section, key, options[section][key].ToString());
                }
            }
            config_file.Flush();
        }

        public void Clean() {
            write_all_values_to_disk();
            config_file.Clean(options);
        }


        public void LoadFromIni() {
            foreach (string section in options.Keys) {
                foreach (string key in options[section].Keys) {
                    if (config_file.KeyExists(section, key)) {
                        switch (options[section][key].value_type) {
                            case ValueType.STRING:
                                options[section][key].set_string(load_string_from_ini(section, key));
                                break;
                            case ValueType.INT:
                                options[section][key].set_int(load_int_from_ini(section, key));
                                break;
                            case ValueType.BOOL:
                                options[section][key].set_bool(load_bool_from_ini(section, key));
                                break;
                            case ValueType.IP:
                                options[section][key].set_ip(load_ip_from_ini(section, key));
                                break;
                            case ValueType.COLOR:
                                options[section][key].set_color(load_color_from_ini(section, key));
                                break;
                        }
                    } else {
                        config_file.Write(section, key, options[section][key].ToString());
                    }

                    Console.WriteLine($"[{options[section][key].value_type.ToString()}] {key} = {options[section][key].ToString()}");
                }
            }

            //write back to unfuck any malformed options
            write_all_values_to_disk();
            config_file.Clean(options);
        }
        string load_string_from_ini(string section, string name) {
            return config_file.Read(section, name);
        }

        int load_int_from_ini(string section, string name) {
            string s = config_file.Read(section, name);
            int res;

            if (int.TryParse(s, out res)) {
                return res;
            }

            Logging.Warning($"Malformed integer value in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            config_file.Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_int();
        }

        bool load_bool_from_ini(string section, string name) {
            string s = config_file.Read(section, name);

            if (s.ToUpper() == "TRUE") return true;
            else if (s.ToUpper() == "FALSE") return false;

            Logging.Warning($"Malformed boolean value in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            config_file.Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_bool();
        }

        byte[] load_ip_from_ini(string section, string name) {
            string s = config_file.Read(section, name);
            string[] split = s.Split('.');
            byte[] ip = new byte[4];

            if (split.Length != 4) goto error;

            for (int i = 0; i < split.Length; i++) {
                int res;

                if (int.TryParse(split[i], out res) && (res >= 0 && res <= 255)) {
                    ip[i] = (byte)res;
                } else goto error;
            }

            return ip;

        error:
            Logging.Warning($"Malformed IP address in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            config_file.Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_ip();
        }

        byte[] load_color_from_ini(string section, string name) {
            string s = config_file.Read(section, name);
            string[] split = s.Split(',');
            byte[] rgba = new byte[4];

            if (split.Length != 4) goto error;

            for (int i = 0; i < split.Length; i++) {
                int res;

                if (int.TryParse(split[i], out res) && (res >= 0 && res <= 255)) {
                    rgba[i] = (byte)res;

                } else goto error;
            }

            return rgba;

        error:
            Logging.Warning($"Malformed color in config option. \"{name}\", value: \"{s}\". Resetting to default.");
            config_file.Write(section, name, options[section][name].ToString());
            return options[section][name].get_default_color();
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

            set_color(new byte[] {default_value.R, default_value.G, default_value.B, default_value.A });
            this.default_value = default_value;
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
                Logging.ErrorAndThrow($"{t.ToString()} is an invalid type!");
            }

            Logging.Info($"Set value of \"{name}\" to \"{as_string}\"");
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
            } else throw new TypeAccessException();
        }
        public void set_color(byte[] value) {
            if (value_type == ValueType.COLOR) {
                if (value.Length == 4) this.value = value;
                else throw new Exception("Wrong number of bytes in color!");
            } else throw new TypeAccessException();
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