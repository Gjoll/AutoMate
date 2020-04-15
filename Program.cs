using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eir.AutoValidate
{
    class Program
    {
        class WatchNode
        {
            public ManualResetEvent wake;
            public String Name;
            public String ExeDir;
            public String ExePath;
            public String ExeArgs;
            public String Filter = "*.*";
            public FileSystemWatcher Watcher;

            public WatchNode()
            {
                Watcher = new FileSystemWatcher();
                wake = new ManualResetEvent(false);
            }

            public void NotifyChange()
            {
                Message(ConsoleColor.DarkGray, $"{this.Name} Directory '{this.Watcher.Path}' changed.");
                this.wake.Set();
            }
        }

        static Boolean showInfo = false;

        Int32 executionCounter = 1;
        List<WatchNode> watchNodes = new List<WatchNode>();

        public Program()
        {
        }

        void ParseArguments(String[] args)
        {
            WatchNode Current()
            {
                if (this.watchNodes.Count == 0)
                    this.watchNodes.Add(new WatchNode());
                return this.watchNodes[this.watchNodes.Count - 1];
            }

            Int32 i = 0;

            String GetArg(String errorMsg, bool dashFlag = false)
            {
                if (i >= args.Length)
                    throw new Exception($"Missing {errorMsg} parameter");

                String arg = args[i++].Trim();
                if ((arg[0] == '"') && (arg[arg.Length - 1] == '"'))
                    arg = arg.Substring(1, arg.Length - 2);

                bool dashParam = arg.StartsWith("-");
                if (dashParam != dashFlag)
                    throw new Exception($"invalid {errorMsg} parameter");

                return arg;
            }

            while (i < args.Length)
            {
                String arg = GetArg("arg", true).ToUpper();
                switch (arg)
                {
                    case "-N":
                        WatchNode w = new WatchNode();
                        if (this.watchNodes.Count > 0)
                            w.ExeDir = this.watchNodes[this.watchNodes.Count - 1].ExeDir;
                        this.watchNodes.Add(w);
                        Current().Name = GetArg("-n");
                        break;

                    case "-F":
                        Current().Filter = GetArg("-f");
                        break;

                    case "-D":
                        Current().ExeDir = GetArg("-d");
                        break;

                    case "-W":
                        Current().Watcher.Path = GetArg("-w");
                        break;

                    case "-C":
                        Current().ExePath = GetArg("-c");
                        break;

                    case "-A":
                        Current().ExeArgs = GetArg("-a");
                        break;

                    default:
                        throw new Exception($"Unknown arg {arg}");
                }
            }
        }

        bool doneFlag = false;

        void RunCommand(WatchNode node)
        {
            while (true)
            {
                try
                {
                    node.wake.WaitOne();
                    node.wake.Reset();

                    // Wait for no events for timeout.
                    bool activityFlag = true;
                    while (activityFlag == true)
                    {
                        if (this.doneFlag)
                            return;
                        activityFlag = node.wake.WaitOne(1000);
                        node.wake.Reset();
                        if (activityFlag)
                        {
                            Message(ConsoleColor.DarkGray,
                                $"{node.Name}: Additional wake events received. Restarting wait");
                        }
                    }

                    this.ExecuteCommand(node);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }


        void ExecuteCommand(WatchNode node)
        {
            using (Mutex gMtx = new Mutex(false, "AutoMate"))
            {
                //Console.Clear();
                DateTime dt = DateTime.Now;
                gMtx.WaitOne(1 * 1000);
                TimeSpan ts = DateTime.Now - dt;
                Message(ConsoleColor.DarkGray,
                    $"{node.Name}: Waited {ts.TotalSeconds} seconds for access");

                Message(ConsoleColor.Green,
                    $"{node.Name}: {executionCounter++}. Executing {node.ExePath} {node.ExeArgs}");
                this.Execute(node.ExeDir, node.ExePath, node.ExeArgs);
            }

            Message(ConsoleColor.DarkGray, "Command complete");
            Message(ConsoleColor.DarkGray, "Press 'q' to quit.");
        }

        static public void Message(String msg)
        {
            String msgLevel = msg.Trim().ToUpper();
            ConsoleColor fgColor = ConsoleColor.White;
            if (msgLevel.StartsWith("INFO"))
            {
                if (showInfo == false)
                    return;
                fgColor = ConsoleColor.White;
            }
            else if (msgLevel.StartsWith("NOTE"))
                fgColor = ConsoleColor.Green;
            else if (msgLevel.StartsWith("WARN"))
                fgColor = ConsoleColor.DarkYellow;
            else if (msgLevel.StartsWith("ERROR"))
                fgColor = ConsoleColor.Red;

            Message(fgColor, msg);
        }

        static public void Message(ConsoleColor fgColor, String msg)
        {
            Console.ForegroundColor = fgColor;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;
        }

        protected Boolean Execute(String workingDir,
            String executablePath,
            String arguments)
        {
            async Task ReadOutAsync(Process p)
            {
                do
                {
                    String s = await p.StandardOutput.ReadLineAsync();
                    s = s?.Replace("\r", "")?.Replace("\n", "")?.Trim();
                    if (String.IsNullOrEmpty(s) == false)
                        Message(ConsoleColor.White, s);
                } while (p.StandardOutput.EndOfStream == false);
            }

            async Task ReadErrAsync(Process p)
            {
                do
                {
                    String s = await p.StandardError.ReadLineAsync();
                    s = s?.Replace("\r", "")?.Replace("\n", "")?.Trim();
                    if (String.IsNullOrEmpty(s) == false)
                        Message(ConsoleColor.Red, s);
                } while (p.StandardError.EndOfStream == false);
            }

            using (Process p = new Process())
            {
                p.StartInfo.FileName = executablePath;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WorkingDirectory = workingDir;
                p.Start();
                Task errTask = ReadErrAsync(p);
                Task outTask = ReadOutAsync(p);
                p.WaitForExit(); // Waits here for the process to exit.    }
                errTask.Wait();
                outTask.Wait();
                return p.ExitCode == 0;
            }
        }

        void Start(WatchNode node)
        {
            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            node.Watcher.NotifyFilter = NotifyFilters.LastAccess
                                   | NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;

            node.Watcher.Filter = node.Filter;
            // Add event handlers.
            node.Watcher.Changed += (sender, args) => node.NotifyChange();
            node.Watcher.Created += (sender, args) => node.NotifyChange();
            node.Watcher.Deleted += (sender, args) => node.NotifyChange();
            node.Watcher.Renamed += (sender, args) => node.NotifyChange();
            node.Watcher.IncludeSubdirectories = true;

            // Begin watching.
            node.Watcher.EnableRaisingEvents = true;

            Task runTask = new Task(() => this.RunCommand(node));
            runTask.Start();
        }

        void WakeAll()
        {
            foreach (WatchNode node in this.watchNodes)
                node.wake.Set();
        }

        void Run()
        {
            foreach (WatchNode node in this.watchNodes)
            {
                Start(node);
                Thread.Sleep(1000);     // let first watches start before starting next ones.
            }

            // Wait for the user to quit the program.
            do
            {
                this.WakeAll();
            } while (Console.Read() != 'q');

            this.doneFlag = true;

            this.WakeAll();
        }

        static Int32 Main(string[] args)
        {
            try
            {
                Program p = new Program();
                p.ParseArguments(args);
                p.Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return -1;
            }
        }
    }
}
