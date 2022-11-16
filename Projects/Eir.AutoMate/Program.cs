using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eir.AutoMate
{
    class Program
    {
        const ConsoleColor colorInfo = ConsoleColor.DarkGray;
        const ConsoleColor colorGood = ConsoleColor.Green;
        const ConsoleColor colorWarn = ConsoleColor.Yellow;
        const ConsoleColor colorError = ConsoleColor.Red;

        class Options
        {
            public class Command
            {
                public String workingDir = ".";
                public String cmdPath = "";
                public String cmdArgs = "";
            }

            public class Watch
            {
                public String name = "?";
                public String filter = "*.*";
                public String[] watchPaths = new String[0];
                public Command[] commands = new Command[0];
            }
            public Watch[] watchs = new Watch[0];

            public Int32 clearScreenTime = 60;
            public bool traceFlag = false;
        }

        class WatchNode
        {
            public DateTime LastPingTime;
            public volatile bool RunFlag = true;
            public Options.Watch Watch;
            public List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();

            public WatchNode(Options.Watch watch)
            {
                this.Watch = watch;
            }
        }

        void NotifyChange(WatchNode node, FileSystemWatcher watcher)
        {
            this.Trace($"{node.Watch.name} '{watcher.Path}' changed.");
            node.RunFlag = true;
            this.wake.Set();
        }

        public ManualResetEvent wake = new ManualResetEvent(false);

        Options options;
        DateTime lastMessageTime = DateTime.Now;

        Boolean showInfo = false;
        bool doneFlag = false;
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
            options = JsonConvert.DeserializeObject<Options>(json);
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


        bool RunOneCommand(out Int32 sleepTime)
        {
            sleepTime = -1;
            foreach (WatchNode node in this.watchNodes)
            {
                // Wait for no events for timeout.
                if (this.doneFlag)
                    return false;

                if (node.RunFlag == true)
                {
                    Int32 timeSinceLastPing = (Int32)(DateTime.Now - node.LastPingTime).TotalMilliseconds;
                    if (timeSinceLastPing < 1000)
                    {
                        sleepTime = (Int32)(1000 - timeSinceLastPing);
                        return false;
                    }

                    try
                    {
                        this.ExecuteCommand(node);
                        node.RunFlag = false;
                        node.LastPingTime = DateTime.Now;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    return true;
                }
            }

            return false;
        }

        void RunCommands(out Int32 sleepTime)
        {
            while (RunOneCommand(out sleepTime))
            {
            }
        }

        void RunCommand()
        {
            Int32 sleepTime = -1;
            while (true)
            {
                this.wake.WaitOne(sleepTime);
                this.wake.Reset();
                this.RunCommands(out sleepTime);
                if (sleepTime == -1)
                {
                    Message(colorInfo, -1, $"'q'->quit");
                    Message(colorInfo, -1, $"'0'or enter->run all.");
                    Int32 counter = 1;
                    foreach (WatchNode node in this.watchNodes)
                    {
                        Message(colorInfo, -1, $"'{counter}' run {node.Watch.name}");
                        counter += 1;
                    }
                }
            }
        }


        void ExecuteCommand(WatchNode node)
        {
            Int32 executionNum = executionCounter++;
            using (Mutex gMtx = new Mutex(false, "AutoMate"))
            {
                DateTime dt = DateTime.Now;
                gMtx.WaitOne(60 * 1000);
                TimeSpan ts = DateTime.Now - dt;
                Trace($"{node.Watch.name}: Waited {ts.TotalSeconds} seconds for access");

                foreach (Options.Command command in node.Watch.commands)
                {
                    Message(ConsoleColor.Green,
                        executionNum,
                        $"{node.Watch.name}: Executing {command.cmdPath} {command.cmdArgs}");
                    this.Execute(executionNum, command.workingDir, command.cmdPath, command.cmdArgs);
                }

                gMtx.ReleaseMutex();
            }
        }

        public void Trace(String msg)
        {
            if (this.options.traceFlag == false)
                return;

            Message(ConsoleColor.Yellow, -1, msg);
        }


        public void Message(Int32 executionNumber,
            String msg)
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
                fgColor = colorGood;
            else if (
                (msgLevel.StartsWith("WARNING") == true) &&
                (msgLevel.StartsWith("WARNINGS:") == false)
                )
                fgColor = colorWarn;
            else if (
                (msgLevel.StartsWith("ERROR") == true) &&
                (msgLevel.StartsWith("ERRORS:") == false)
                )
                fgColor = colorError;
            else if (msgLevel.StartsWith("INFO"))
                fgColor = colorInfo;

            Message(fgColor, executionNumber, msg);
        }

        public void Message(ConsoleColor fgColor,
                            Int32 executionNumber,
                            String msg)
        {
            lock (typeof(Program))
            {
                DateTime now = DateTime.Now;
                Int32 minutes = this.options.clearScreenTime / 60;
                Int32 seconds = this.options.clearScreenTime % 60;
                DateTime msgTimeout = lastMessageTime + new TimeSpan(0, minutes, seconds);
                if (now > msgTimeout)
                    Console.Clear();
                lastMessageTime = now;

                Console.ForegroundColor = colorInfo;
                if (executionNumber > 0)
                    Console.Write($"[{executionNumber}] ");

                Console.ForegroundColor = fgColor;
                Console.WriteLine(msg);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        protected Boolean Execute(Int32 executionNum,
            String workingDir,
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
                        Message(executionNum, s);
                } while (p.StandardOutput.EndOfStream == false);
            }

            async Task ReadErrAsync(Process p)
            {
                do
                {
                    String s = await p.StandardError.ReadLineAsync();
                    s = s?.Replace("\r", "")?.Replace("\n", "")?.Trim();
                    if (String.IsNullOrEmpty(s) == false)
                        Message(ConsoleColor.Red, executionNum, s);
                } while (p.StandardError.EndOfStream == false);
            }

            Environment.SetEnvironmentVariable("ApplicationData", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            Environment.SetEnvironmentVariable("LocalApplicationData", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            Environment.SetEnvironmentVariable("MyDocuments", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            Environment.SetEnvironmentVariable("ProgramFiles", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            Environment.SetEnvironmentVariable("ProgramFilesX86", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            Environment.SetEnvironmentVariable("Programs", Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            Environment.SetEnvironmentVariable("System", Environment.GetFolderPath(Environment.SpecialFolder.System));
            Environment.SetEnvironmentVariable("SystemX86", Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
            Environment.SetEnvironmentVariable("UserProfile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            Environment.SetEnvironmentVariable("Windows", Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            workingDir = Environment.ExpandEnvironmentVariables(workingDir);
            executablePath = Environment.ExpandEnvironmentVariables(executablePath);
            arguments = Environment.ExpandEnvironmentVariables(arguments);

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
            foreach (String watchPath in node.Watch.watchPaths)
            {
                try
                {
                    FileSystemWatcher watcher = new FileSystemWatcher();
                    node.Watchers.Add(watcher);
                    watcher.Path = watchPath;
                    // Watch for changes in LastAccess and LastWrite times, and
                    // the renaming of files or directories.
                    watcher.NotifyFilter = NotifyFilters.LastAccess
                                           | NotifyFilters.LastWrite
                                           | NotifyFilters.FileName
                                           | NotifyFilters.DirectoryName;

                    watcher.Filter = node.Watch.filter;
                    // Add event handlers.
                    watcher.Changed += (sender, args) => NotifyChange(node, watcher);
                    watcher.Created += (sender, args) => NotifyChange(node, watcher);
                    watcher.Deleted += (sender, args) => NotifyChange(node, watcher);
                    watcher.Renamed += (sender, args) => NotifyChange(node, watcher);
                    watcher.IncludeSubdirectories = true;

                    // Begin watching.
                    watcher.EnableRaisingEvents = true;
                }
                catch(Exception err)
                {
                    Message(colorError, -1, $"Error creating watcher on path '{watchPath}'");
                }
            }
        }

        void Run()
        {
            foreach (Options.Watch watch in this.options.watchs)
            {
                if (String.IsNullOrEmpty(watch.name))
                    throw new Exception("Watch field 'name' must be set");
                if (watch.watchPaths.Length == 0)
                    throw new Exception($"Watch '{watch.name}' field 'watchPaths' is not set");
                if ((watch.commands == null) || (watch.commands.Length == 0))
                    throw new Exception($"Watch '{watch.name}' field no commands defined");
                foreach (Options.Command command in watch.commands)
                {
                    if (String.IsNullOrEmpty(command.cmdPath))
                        throw new Exception($"Watch '{watch.name}' field 'cmdPath' is not set");
                }
                WatchNode node = new WatchNode(watch);
                this.watchNodes.Add(node);
                Start(node);
            }

            Task runTask = new Task(() => this.RunCommand());
            runTask.Start();

            void RunAll()
            {
                foreach (WatchNode node in this.watchNodes)
                    node.RunFlag = true;
                this.wake.Set();
            }

            void RunOne(Int32 selection)
            {
                if (selection == 0)
                    RunAll();
                else if ((selection > 0) && (selection <= this.watchNodes.Count))
                {
                    WatchNode node = this.watchNodes[selection - 1];
                    node.RunFlag = true;
                    this.wake.Set();
                }
            }

            foreach (WatchNode node in this.watchNodes)
                node.LastPingTime = DateTime.Now;

            RunAll();
            // Wait for the user to quit the program.
            while (true)
            {
                String s = Console.ReadLine().Trim().ToLower();
                switch (s)
                {
                    case "q":
                        return;

                    default:
                        Console.Clear();
                        if (Int32.TryParse(s, out Int32 selection))
                            RunOne(selection);
                        else
                            RunAll();
                        break;
                }
            }
        }

        static Int32 Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
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
