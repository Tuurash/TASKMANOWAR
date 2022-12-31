using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PidHandler
{
    internal class Program
    {
        public static Port portDetails { get; set; }
        public static Process SelectedProcess { get; set; }

        static void Main(string[] args)
        {
            Task t = MainAsync(args);
            t.Wait();
        }

        private static async Task MainAsync(string[] args)
        {
            Console.WriteLine("Enter PID you want to Kill");

            int SelectedPort = int.Parse(Console.ReadLine());

            SelectedProcess = await Task.FromResult(Process.GetProcessById(SelectedPort));
            await StartKillProcess(SelectedProcess);

        }

        private static async Task StartKillProcess(Process selectedProcess) =>
            //portDetails = RunningPorts.FirstOrDefault(x => x.PID == SelectedPort.ToString());
            await KillProcessByPID(SelectedProcess.Id);

        private static async Task KillProcessByPID(int pid)
        {
            try
            {
                ManagementObjectSearcher processSearcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
                ManagementObjectCollection processCollection = processSearcher.Get();

                // We must kill child processes first!
                if (processCollection != null)
                {
                    foreach (ManagementObject mo in processCollection)
                    {
                        await KillProcessByPID(Convert.ToInt32(mo["ProcessID"])); //kill child processes(also kills childrens of childrens etc.)
                    }
                }

                // Then kill parents.
                try
                {
                    foreach (var process in Process.GetProcessesByName(SelectedProcess.ProcessName))
                    {
                        await Task.Run(() =>
                        {
                            try
                            {
                                process.Kill();
                                process.Close();
                            }
                            catch { }
                        });
                    }
                    //if (!proc.HasExited) proc.Kill();
                }
                catch (ArgumentException)
                {
                    // Process already exited.
                }
            }
            catch (Exception ex)
            {
                await TryKillingTask(portDetails);
            }
            finally
            {
                await ClosingConfirmation();
            }
        }

        private static async Task TryKillingTask(Port proc)
        {
            await Task.Run(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = proc.name,
                        Arguments = $"/im " + proc.name + ".exe /f /t",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }).WaitForExit();
                }
                catch (Exception ex)
                {

                }
            });


        }

        private static async Task ClosingConfirmation()
        {
            string Confirmation = Console.ReadLine().ToLower();

            if (Confirmation.ToLower() == "end")
                Environment.Exit(0);
            else
            {
                int newPID = 0;
                if (int.TryParse(Confirmation, out newPID))
                {
                    SelectedProcess = Process.GetProcessById(newPID);
                    await StartKillProcess(SelectedProcess);
                }
            }

        }

        public static List<Port> GetNetStatPorts()
        {
            var Ports = new List<Port>();

            try
            {
                using (Process p = new Process())
                {

                    ProcessStartInfo ps = new ProcessStartInfo();
                    ps.Arguments = "-a -n -o";
                    ps.FileName = "netstat.exe";
                    ps.UseShellExecute = false;
                    ps.WindowStyle = ProcessWindowStyle.Hidden;
                    ps.RedirectStandardInput = true;
                    ps.RedirectStandardOutput = true;
                    ps.RedirectStandardError = true;

                    p.StartInfo = ps;
                    p.Start();

                    StreamReader stdOutput = p.StandardOutput;
                    StreamReader stdError = p.StandardError;

                    string content = stdOutput.ReadToEnd() + stdError.ReadToEnd();
                    string exitStatus = p.ExitCode.ToString();

                    if (exitStatus != "0")
                    {
                        // Command Errored. Handle Here If Need Be
                    }

                    //Get The Rows
                    string[] rows = Regex.Split(content, "\r\n");
                    foreach (string row in rows)
                    {
                        //Split it baby
                        string[] tokens = Regex.Split(row, "\\s+");
                        if (tokens.Length > 4 && (tokens[1].Equals("UDP") || tokens[1].Equals("TCP")))
                        {
                            string localAddress = Regex.Replace(tokens[2], @"\[(.*?)\]", "1.1.1.1");
                            Ports.Add(new Port
                            {
                                protocol = localAddress.Contains("1.1.1.1") ? String.Format("{0}v6", tokens[1]) : String.Format("{0}v4", tokens[1]),
                                port_number = localAddress.Split(':')[1],
                                process_name = tokens[1] == "UDP" ? LookupProcess(Convert.ToInt16(tokens[4])) : LookupProcess(Convert.ToInt16(tokens[5])),
                                PID = tokens[5]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
            }
            return Ports;
        }

        public static string LookupProcess(int pid)
        {
            string procName;
            try { procName = Process.GetProcessById(pid).ProcessName; }
            catch (Exception) { procName = "-"; }
            return procName;
        }
    }

    public class Port
    {
        public string name
        {
            get
            {
                return string.Format("{0} ({1} port {2})", this.process_name, this.protocol, this.port_number);
            }
            set { }
        }
        public string port_number { get; set; }
        public string process_name { get; set; }
        public string protocol { get; set; }
        public string PID { get; set; }
    }
}
