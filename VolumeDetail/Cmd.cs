using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace VolumeDetail
{
    internal class Cmd
    {
        public async Task<string> GetCommandOutputAsync(string command)
        {
            await Task.Delay(1);

            string output = "";
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", $@"/C {command}")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };
            process.OutputDataReceived += (sender, args) => output += $"{args.Data}\n";

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            return output;
        }

        public async Task RunCommand(string command, bool shell = true)
        {
            await Task.Delay(1);

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", $@"/C {command}")
                {
                    UseShellExecute = shell
                }
            };

            process.Start();
            process.WaitForExit();
        }
    }
}
