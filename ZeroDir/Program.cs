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
                server_thread.Start(0.ToString());                
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

                } else if (line == "threadstatus") {
                    for (int i = 0; i < servers.Count; i++) {
                        var port = CurrentConfig.server.values["server"]["port"].get_int();
                        var p = CurrentConfig.server.values["server"]["prefix"].ToString().Trim().Split(' ')[0];

                        if (p.StartsWith("http://")) p = p.Remove(0, 7);
                        if (p.StartsWith("https://")) p = p.Remove(0, 8);
                        if (p.EndsWith('/')) p = p.Remove(p.Length - 1, 1);

                        FolderServer s = servers[i];
                        Logging.Message($"[Server] {p}:{port}");
                        for (int n = 0; n < s.dispatch_threads.Length; n++) {
                            Thread t = s.dispatch_threads[n];
                            Logging.Message($"| [Name] {t.Name} [IsAlive] {t.IsAlive} [ThreadState] {t.ThreadState.ToString()}");
                        }
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

        static void start_server(object? id) {
            servers[servers.Count - 1].StartServer(id.ToString());
        }
        ~Program() {
            Console.CursorVisible = true;
            Console.ForegroundColor = ConsoleColor.White;
        }

    }
}
