using System.Drawing;
using ZeroDir.Config;

namespace ZeroDir
{
    internal class Program {
        static List<FolderServer> servers = new List<FolderServer>();       

        static void Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            CurrentConfig.server = new ServerConfig("server");
            CurrentConfig.shares = new FileShareConfig("shares");
            
            if (CurrentConfig.shares.share_count == 0) {
                Logging.Message($"No shares configured in shares file!");
            }

            //foreach (string section in CurrentConfig.shares.Keys) {
                servers.Add(new FolderServer());
                Thread server_thread = new Thread(new ParameterizedThreadStart(start_server));
                server_thread.Start();                
            //}

            while (true) {
                string line = Console.ReadLine();

                if (line == "restart") {
                    Logging.Message("Restarting server!");
                    for (int i = 0; i < servers.Count; i++) {
                        servers[i].StopServer();                        
                    }

                    CurrentConfig.server = new ServerConfig("server");
                    CurrentConfig.shares = new FileShareConfig("shares");

                    for (int i = 0; i < servers.Count; i++) {
                        servers[i] = new FolderServer();
                        server_thread = new Thread(new ParameterizedThreadStart(start_server));
                        server_thread.Start();
                    }

                } else if (line.StartsWith("$") && line.Contains('.') && line.Contains('=')) {
                    line = line.Remove(0, 1);
                    CurrentConfig.server.config_file.ChangeValueByString(CurrentConfig.server.values, line);
                } else if (line.StartsWith("#") && line.Contains('.') && line.Contains('=')) {
                    line = line.Remove(0, 1);
                    CurrentConfig.shares.config_file.ChangeValueByString(CurrentConfig.shares, line);
                }
            }
            CurrentConfig.server.Clean();
        }

        static void start_server(object? section) {
            servers[servers.Count - 1].StartServer();
        }
        ~Program() {
            Console.CursorVisible = true;
            Console.ForegroundColor = ConsoleColor.White;
        }

    }
}
