using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
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
        }

        bool doneFlag = false;
        Options options;

        class WatchNode
        {
            public Options.Watch Watch;
            public ManualResetEvent wake;
            public List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();

            public WatchNode(Options.Watch watch)
            {
                this.Watch = watch;
                this.wake = new ManualResetEvent(false);
            }

            public void NotifyChange(FileSystemWatcher watcher)
            {
                Trace($"{this.Watch.name} Directory '{watcher.Path}' changed.");
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
                            Trace($"{node.Watch.name}: Additional wake events received. Restarting wait");
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
                Trace($"{node.Watch.name}: Waited {ts.TotalSeconds} seconds for access");

                foreach (Options.Command command in node.Watch.commands)
                {
                    Message(ConsoleColor.Green,
                        $"{node.Watch.name}: {executionCounter++}. Executing {command.cmdPath} {command.cmdArgs}");
                    this.Execute(command.workingDir, command.cmdPath, command.cmdArgs);
                }
            }

            Message(ConsoleColor.DarkGray, "Command complete");
            Message(ConsoleColor.DarkGray, "Press 'q' to quit.");
        }

        static public void Trace(String msg)
        {
            //Message(ConsoleColor.DarkGray, msg);
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
                watcher.Changed += (sender, args) => node.NotifyChange(watcher);
                watcher.Created += (sender, args) => node.NotifyChange(watcher);
                watcher.Deleted += (sender, args) => node.NotifyChange(watcher);
                watcher.Renamed += (sender, args) => node.NotifyChange(watcher);
                watcher.IncludeSubdirectories = true;

                // Begin watching.
                watcher.EnableRaisingEvents = true;
            }

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
                if (watch.watchPaths.Length == 0)
                    throw new Exception($"Watch '{watch.name}' field 'watchPaths' is not set");
                if ((watch.commands == null) ||  (watch.commands.Length == 0))
                    throw new Exception($"Watch '{watch.name}' field no commands defined");
                foreach (Options.Command command in watch.commands)
                {
                    if (String.IsNullOrEmpty(command.cmdPath))
                        throw new Exception($"Watch '{watch.name}' field 'cmdPath' is not set");
                }

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
