using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using LuaInterface;
using Newtonsoft.Json.Linq;
using Udp;
using Timer = System.Threading.Timer;

namespace HelloRust
{
    public partial class Console : Form
    {
        Queue<Tuple<PumpTaskType, object>> messageQueue;
        SemaphoreSlim semaphore;

        UdpFilter udp;
        Lua lua;
        LuaFunction filterFunction;
        public event Action<string, bool, string> OnCheckBoxChanged;

        FileSystemWatcher watcher;
        Stopwatch updateStopwatch = new Stopwatch();
        BackgroundWorker scriptUpdater = new BackgroundWorker();
        HashSet<string> affectedFiles = new HashSet<string>();

        object luaLocker = new object();

        bool scriptExists = false;
        int checkBoxHeight;

        Dictionary<string, CheckBoxContainer> _checkboxes = new Dictionary<string, CheckBoxContainer>();

        class CheckBoxContainer
        {
            public CustomCheckBox checkBox;
            public bool Checked { set; get; }
            public bool Marked { set; get; }
            public string Group { set; get; }
        }

        public Action<string, bool, string> OnCheckBoxChangedAdd(Action<string, bool, string> action)
        {
            OnCheckBoxChanged += action;
            return action;
        }

        public void OnCheckBoxChangedRemove(Action<string, bool, string> action)
        {
            OnCheckBoxChanged -= action;
        }

        public Console()
        {
            InitializeComponent();
            CheckBox cb = new CheckBox { Text = "Test", Parent = controlsPanel };
            cb.AutoSize = true;
            checkBoxHeight = cb.Height;
            cb.Parent = null;
            Timer timer = new Timer(obj => { Initialize(); }, null, 500, Timeout.Infinite);
        }

        public void AddCheckBox(string name, string title, int x, int y, bool _checked, string group = null)
        {
            controlsPanel.InvokeIfRequired(() =>
            {
                if (!_checkboxes.ContainsKey(name))
                {
                    CheckBoxContainer container = new CheckBoxContainer();
                    if (group != null && !_checkboxes.Any(p => p.Value.Group == group && p.Value.Checked))
                        container.Checked = true;
                    else
                        container.Checked = _checked;
                    container.checkBox = new CustomCheckBox
                    {
                        Checked = container.Checked,
                        Text = title,
                        Tag = name,
                        Left = 12 + x,
                        Top = 12 + y,
                        Parent = controlsPanel,
                        AutoSize = true
                    };

                    container.Marked = true;
                    container.Group = group;

                    if (_checked && group != null)
                    {
                        foreach (var entry in _checkboxes)
                            if (entry.Value.Group == container.Group)
                            {
                                entry.Value.Checked = false;
                                entry.Value.checkBox.SafeChecked = false;
                            }
                    }

                    _checkboxes.Add(name, container);
                    container.checkBox.CheckedChanged += (_sender, _e) =>
                    {
                        CustomCheckBox cb = _sender as CustomCheckBox;
                        var con = _checkboxes[(string)cb.Tag] as CheckBoxContainer;

                        con.Checked = cb.Checked;

                        if (con.Group != null)
                        {
                            if (cb.Checked)
                            {
                                foreach (var entry in _checkboxes)
                                    if (entry.Value.Group == con.Group)
                                    {
                                        entry.Value.Checked = false;
                                        entry.Value.checkBox.SafeChecked = false;
                                    }
                                cb.SafeChecked = true;
                                con.Checked = true;
                            }
                            else
                            {
                                if (!_checkboxes.Any(p => p.Value.Group == con.Group && p.Value.Checked))
                                {
                                    cb.SafeChecked = true;
                                    con.Checked = true;
                                }
                            }
                        }
                        
                        if (OnCheckBoxChanged != null)
                        {
                            try
                            {
                                OnCheckBoxChanged(name, con.Checked, con.Group);
                            }
                            catch { }
                        }
                    };
                }
                else
                {
                    CheckBoxContainer container = _checkboxes[name];
                    container.checkBox.Text = title;
                    container.checkBox.Left = 12 + x;
                    container.checkBox.Top = 12 + y;
                    container.Marked = true;
                    container.Group = group;
                }
            });
        }

        public bool GetCheckBoxState(string name)
        {
            if (_checkboxes.ContainsKey(name))
            {
                return (_checkboxes[name] as CheckBoxContainer).Checked;
            }
            return false;
        }

        public string GetCheckBoxGroupState(string name)
        {
            return _checkboxes.Where(p => p.Value.Group == name && p.Value.Checked).ElementAtOrDefault(0).Key;
        }

        void Initialize()
        {
            messageQueue = new Queue<Tuple<PumpTaskType, object>>(1000);
            semaphore = new SemaphoreSlim(0, 1000);
            messagePump.RunWorkerAsync();

            WriteLine("Hello, Rust?");

            try
            {
                JObject json = JObject.Parse(File.ReadAllText("configuration.json"));
                string remoteIp = json["remote_ip"].ToObject<string>();
                int port = json["remote_port"].ToObject<int>();
                int localPort = json["local_port"].ToObject<int>();
                int mtu = json["mtu"].ToObject<int>();
                int natLifeTime = json["nat_life_time"].ToObject<int>();
                WriteLine("Set up UDP filter on " + remoteIp + ":" + port.ToString() + ". Local port: " + localPort.ToString());
                udp = new UdpFilter(IPAddress.Parse(remoteIp), port, localPort, mtu);
                udp.NatLifeTime = natLifeTime;
                udp.PacketFilter += UdpPacketFilter;

                scriptUpdater.DoWork += ScriptUpdate;
                scriptUpdater.RunWorkerAsync();

                Write("Loading scripts...");
                Tuple<bool, Exception> reloadResult = ReloadScripts();
                scriptExists = reloadResult.Item1;
                WriteLine((scriptExists) ? "successfully" : reloadResult.Item2.Message);

                Write("Initializing hot switch...");
                watcher = new FileSystemWatcher("scripts", "*.lua");
                watcher.IncludeSubdirectories = true;
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                watcher.Changed += WatcherHandler;
                watcher.Created += WatcherHandler;
                watcher.Deleted += WatcherHandler;
                watcher.Renamed += WatcherHandler;
                watcher.EnableRaisingEvents = true;

                WriteLine("done");

            }
            catch (Exception ex)
            {
                WriteLine(string.Format("Exception: {0}\n{1}\nPlease restart program.", ex.Message, ex.StackTrace));
                return;
            }
        }

        void ScriptUpdate(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (updateStopwatch.ElapsedMilliseconds >= 1000)
                {
                    updateStopwatch.Reset();
                    Write("Performing hot switch...");
                    lock (affectedFiles)
                    {
                        foreach (var file in affectedFiles)
                        {
                            if (File.Exists(file))
                            {
                                while (true)
                                {
                                    try
                                    {
                                        using (var stream = File.OpenRead(file))
                                        {
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                    break;
                                }
                            }
                        }
                        affectedFiles.Clear();
                    }
                    Tuple<bool, Exception> reloadResult;
                    lock (luaLocker)
                    {
                        reloadResult = ReloadScripts();
                        scriptExists = reloadResult.Item1;
                    }

                    WriteLine((scriptExists) ? "successfully" : reloadResult.Item2.Message);
                }
                Thread.Sleep(100);
            }
        }

        void WatcherHandler(object sender, FileSystemEventArgs e)
        {
            lock (affectedFiles)
            {
                affectedFiles.Add("scripts/" + e.Name);
            }
            if (!updateStopwatch.IsRunning)
                updateStopwatch.Start();
            updateStopwatch.Restart();
        }

        void UdpPacketFilter(Packet packet)
        {
            lock (luaLocker)
            {
                if (scriptExists)
                {
                    try
                    {
                        filterFunction.Call(packet);
                    }
                    catch (Exception ex)
                    {
                        WriteLine("Exception was rised during filter call: " + ex.Message);
                    }
                }
            }
        }

        Tuple<bool, Exception> ReloadScripts()
        {
            try
            {
                if (File.Exists("scripts/main.lua"))
                {
                    if (lua != null)
                    {
                        if (filterFunction != null)
                        {
                            try
                            {
                                LuaFunction finalize = lua.GetFunction("finalize");
                                if (finalize != null)
                                    finalize.Call();
                            }
                            catch (Exception ex)
                            {
                                WriteLine("Exception was rised during finalization: " + ex.Message);
                            }
                        }
                        foreach (var c in _checkboxes)
                        {
                            c.Value.Marked = false;
                        }
                        lua.Close();
                    }
                    OnCheckBoxChanged = null;
                    lua = new Lua();

                    lua.RegisterFunction("print", this, typeof(Console).GetMethod("WriteLine"));
                    lua.RegisterFunction("write", this, typeof(Console).GetMethod("Write"));
                    lua["panel"] = controlsPanel;
                    lua.NewTable("checkbox");
                    lua.RegisterFunction("checkbox.add", this, typeof(Console).GetMethod("AddCheckBox"));
                    lua.RegisterFunction("checkbox.get", this, typeof(Console).GetMethod("GetCheckBoxState"));
                    lua["checkbox.height"] = checkBoxHeight;
                    lua.RegisterFunction("checkbox.getgroup", this, typeof(Console).GetMethod("GetCheckBoxGroupState"));
                    lua.NewTable("checkbox.changed");
                    lua.RegisterFunction("checkbox.changed.add", this, typeof(Console).GetMethod("OnCheckBoxChangedAdd"));
                    lua.RegisterFunction("checkbox.changed.remove", this, typeof(Console).GetMethod("OnCheckBoxChangedRemove"));
                    lua.DoFile("scripts/main.lua");
                    filterFunction = lua.GetFunction("main");
                    if (filterFunction != null)
                    {
                        LuaFunction initialize = lua.GetFunction("initialize");
                        if (initialize != null)
                            initialize.Call();
                        List<string> remove = new List<string>();
                        foreach (var c in _checkboxes)
                        {
                            if (!c.Value.Marked)
                                remove.Add(c.Key);
                        }
                        messageQueue.Enqueue(new Tuple<PumpTaskType,object>(PumpTaskType.RemoveCheckBoxes, remove));
                        semaphore.Release();
                        return new Tuple<bool, Exception>(true, null);
                    }
                    return new Tuple<bool, Exception>(false, new InvalidOperationException("Warning: \"main\" function doesn't exist in the main.lua."));
                }
                return new Tuple<bool, Exception>(false, new FileNotFoundException("File \"scripts/main.lua\" doesn't exist.", "scripts/main.lua"));
            }
            catch (Exception ex)
            {
                return new Tuple<bool, Exception>(false, ex);
            }
        }

        public void Write(string message)
        {
            lock (messageQueue)
            {
                messageQueue.Enqueue(new Tuple<PumpTaskType, object>(PumpTaskType.Write, message));
            }
            semaphore.Release();
        }

        public void WriteLine(string message)
        {
            lock (messageQueue)
            {
                messageQueue.Enqueue(new Tuple<PumpTaskType, object>(PumpTaskType.WriteLine, message));
            }
            semaphore.Release();
        }

        private void messagePump_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                semaphore.Wait();
                Tuple<PumpTaskType, object> tuple;
                lock (messageQueue)
                {
                    tuple = messageQueue.Dequeue();
                }

                try
                {
                    switch (tuple.Item1)
                    {
                        case PumpTaskType.Write:
                            textBox.InvokeIfRequired(() =>
                            {
                                textBox.AppendText(((string)tuple.Item2).Replace("\n", "\r\n"));
                            });
                            break;

                        case PumpTaskType.WriteLine:
                            textBox.InvokeIfRequired(() =>
                            {
                                textBox.AppendLine(((string)tuple.Item2).Replace("\n", "\r\n"));
                            });
                            break;
                        case PumpTaskType.RemoveCheckBoxes:
                            controlsPanel.InvokeIfRequired(() =>
                            {
                                (tuple.Item2 as List<String>).ForEach(s => 
                                {
                                    _checkboxes[s].checkBox.Parent = null;
                                    _checkboxes.Remove(s);
                                });
                            });
                            break;
                    }
                }
                catch { }
            }
        }

    }

    public enum PumpTaskType
    {
        Write,
        WriteLine,
        RemoveCheckBoxes
    }

    public class CustomCheckBox : CheckBox
    {
        public bool RaiseEvents { set; get; }

        public bool SafeChecked
        {
            set
            {
                RaiseEvents = false;
                Checked = value;
                RaiseEvents = true;
            }
            get
            {
                return Checked;
            }
        }

        public CustomCheckBox()
        {
            RaiseEvents = true;
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            if (RaiseEvents)
                base.OnCheckedChanged(e);
        }
    }
}
