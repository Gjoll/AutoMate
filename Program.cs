using Newtonsoft.Json;
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
        class Options
        {
            public class Watch
            {
                public String name = "?";
                public String filter = "*.*";
                public String watchPath = "";
                public String workingDir = ".";
                public String cmdPath = "";
                public String cmdArgs = "";
            }
            public Watch[] watchs;
        }

        bool doneFlag = false;
        Options options;

        class WatchNode
        {
            public Options.Watch Watch;
            public ManualResetEvent wake;
            public FileSystemWatcher Watcher;

            public WatchNode(Options.Watch watch)
            {
                this.Watch = watch;
                this.Watcher = new FileSystemWatcher();
                this.wake = new ManualResetEvent(false);
            }

            public void NotifyChange()
            {
                Message(ConsoleColor.DarkGray, $"{this.Watch.name} Directory '{this.Watcher.Path}' changed.");
                this.wake.Set();
            }
        }

        static Boolean showInfo = false;

        Int32 executionCounter = 1;
        List<WatchNode> watchNodes = new List<WatchNode>();

        public Program()
        {
        }

        //void CreateOptions(String path)
        //{
        //    Options o = new Options();
        //    o.watchs= new Options.Watch[]
        //    {
        //            new Options.Watch()
        //            {
        //                name = "name",
        //                cmdPath = "cmdPath"
        //            }
        //    };
        //    String j = JsonConvert.SerializeObject(o);
        //    File.WriteAllText(path, j);
        //}

        void ParseCommands(String path)
        {
            //CreateOptions(@"c:\Temp\AutoMate.json");
            String fullPath = Path.GetFullPath(path);
            if (File.Exists(path) == false)
                throw new Exception($"Options file {path} not found");
            String json = File.ReadAllText(path);
            this.options = JsonConvert.DeserializeObject<Options>(json);
            if (
                (this.options.watchs == null) ||
                (this.options.watchs.Length == 0)
                )
                throw new Exception("No watches defined");
        }

        void ParseArguments(String[] args)
        {
            switch (args.Length)
            {
                case 0:
                    ParseCommands("automate.json");
                    break;
                case 1:
                    ParseCommands(args[0]);
                    break;
                default:
                    throw new Exception($"Unexpected parameters");
            }
        }


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
                                $"{node.Watch.name}: Additional wake events received. Restarting wait");
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
                    $"{node.Watch.name}: Waited {ts.TotalSeconds} seconds for access");

                Message(ConsoleColor.Green,
                    $"{node.Watch.name}: {executionCounter++}. Executing {node.Watch.cmdPath} {node.Watch.cmdArgs}");
                this.Execute(node.Watch.workingDir, node.Watch.cmdPath, node.Watch.cmdArgs);
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
            else if (
                (msgLevel.StartsWith("WARNING") == true) &&
                (msgLevel.StartsWith("WARNINGS:") == false)
                )
                fgColor = ConsoleColor.Yellow;
            else if (
                (msgLevel.StartsWith("ERROR") == true) &&
                (msgLevel.StartsWith("ERRORS:") == false)
                )
                fgColor = ConsoleColor.Red;
            else if (msgLevel.StartsWith("INFO"))
                fgColor = ConsoleColor.DarkGray;

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
                        Message(s);
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
            node.Watcher.Path = node.Watch.watchPath;
            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            node.Watcher.NotifyFilter = NotifyFilters.LastAccess
                                   | NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;

            node.Watcher.Filter = node.Watch.filter;
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
            foreach (Options.Watch watch in this.options.watchs)
            {
                if (String.IsNullOrEmpty(watch.name))
                    throw new Exception("Watch field 'name' must be set");
                if (String.IsNullOrEmpty(watch.watchPath))
                    throw new Exception($"Watch '{watch.name}' field 'watchPath' is not set");
                if (String.IsNullOrEmpty(watch.cmdPath))
                    throw new Exception($"Watch '{watch.name}' field 'cmdPath' is not set");

                WatchNode node = new WatchNode(watch);
                this.watchNodes.Add(node);
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
