using System.Drawing;

namespace ZeroDir {
    internal class Program {

        static void Main(string[] args) {
            Server.config = new Configuration("config", new Dictionary<string, Dictionary<string, ConfigValue>>() {
            { "server",
                new Dictionary<string, ConfigValue>() {
                    { "bind_to_address", new ConfigValue(new byte[]{127,0,0,1})},
                    { "bind_to_port", new ConfigValue(8080)},
                    { "db_location", new ConfigValue("metadata.db")},
                    { "folder", new ConfigValue("W://")},
                    { "URL", new ConfigValue("http://localhost:8080")}
                }
            },
            { "UI",
                new Dictionary<string, ConfigValue>() {
                    {"background_color", new ConfigValue(Color.Red)}
                }
            }});
            Logging.Warning(Server.config.options["server"]["folder"].ToString());
            HttpServer.start(Server.config.options["server"]["folder"].get_string());

            Server.config.Clean();
        }
    }
}
