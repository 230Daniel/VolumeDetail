using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VolumeDetail
{
    internal class Program
    {
        private static Cmd _cmd = new Cmd();
        private static List<Volume> errors = new List<Volume>();

        static void Main()
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            Console.Title = "Volume Detail";

            await LoginAsync();
            List<Volume> volumes = await GetAllVolumesAsync();
            await DetailVolumesAsync(volumes);
            OutputErrors();
            Save(volumes);
        }

        private static async Task LoginAsync()
        {
            Console.WriteLine("Are you logged into IBM Cloud? (y/n)");
            ConsoleKeyInfo key = new ConsoleKeyInfo();
            while(!(key.Key == ConsoleKey.N || key.Key == ConsoleKey.Y))
            {
                key = Console.ReadKey();
                if (key.Key == ConsoleKey.N)
                {
                    Console.WriteLine("\nPlease log in using the shell window.");
                    await _cmd.RunCommand("ibmcloud login -sso");
                }
                if(key.Key == ConsoleKey.Y)
                {
                    Console.WriteLine();
                }
            }

            Console.Clear();
        }

        private static async Task<List<Volume>> GetAllVolumesAsync()
        {
            Console.Write("Getting volumes...");

            Task<List<Volume>> fileTask = GetVolumesAsync("file");
            Task<List<Volume>> blockTask = GetVolumesAsync("block");

            await Task.WhenAll(fileTask, blockTask);

            List<Volume> volumes = new List<Volume>();
            volumes.AddRange(fileTask.Result);
            volumes.AddRange(blockTask.Result);

            Console.WriteLine(" done");

            return volumes;
        }

        private static async Task<List<Volume>> GetVolumesAsync(string type)
        {
            string commandOut = await _cmd.GetCommandOutputAsync($"ibmcloud sl {type} volume-list");

            List<Volume> volumes = new List<Volume>();

            string[] foundVolumes = commandOut.Split("\n").Skip(1).ToArray();
                
            foreach (string volume in foundVolumes)
            {
                try
                {
                    int volumeId = int.Parse(volume.Split(" ").First());
                    volumes.Add(new Volume(volumeId, $"{type}"));
                }
                catch { }
            }

            return volumes;
        }

        private static double _completed;
        private static async Task DetailVolumesAsync(List<Volume> volumes)
        {
            Console.Write("Detailing volumes... ");
            List<Task> tasks = new List<Task>();

            Task bar = ProgressBar(volumes.Count);

            foreach (Volume volume in volumes)
            {
                tasks.Add(DetailVolumeAsync(volume));
                await Task.Delay(1);
            }
            await Task.WhenAll(tasks);

            await bar;
        }

        private static async Task ProgressBar(int total)
        {
            using (ProgressBar progress = new ProgressBar()) 
            {
                while (_completed < total)
                {
                    progress.Report((double) _completed / total);
                    await Task.Delay(100);
                }
            }

            Console.WriteLine("done");
        }

        private static async Task DetailVolumeAsync(Volume volume)
        {
            await Task.Delay(1);
            _completed += 0.25;

            try
            {
                string commandOut =
                    await _cmd.GetCommandOutputAsync($"ibmcloud sl {volume.Type} volume-detail {volume.Id}");
                string[] outputs = commandOut.Replace(" ", "").Replace("   ", "").Split("\n");

                string row = outputs.First(x => x.StartsWith("Username"));
                volume.Username = ReplaceFirst(row, "Username", "");

                row = outputs.First(x => x.StartsWith("Capacity(GB)"));
                volume.Capacity = decimal.Parse(ReplaceFirst(row, "Capacity(GB)", ""));

                row = outputs.First(x => x.StartsWith("EnduranceTierPerIOPS"));
                volume.EnduranceTierPerIops = decimal.Parse(ReplaceFirst(row, "EnduranceTierPerIOPS", ""));

                row = outputs.First(x => x.StartsWith("Datacenter"));
                volume.Datacenter = ReplaceFirst(row, "Datacenter", "");

                if (volume.Type == "file")
                {
                    await DetailSnapshotAsync(volume);
                }
            }
            catch
            {
                errors.Add(volume);
            }

            _completed += 0.75;
        }

        private static async Task DetailSnapshotAsync(Volume fileVolume)
        {
            await Task.Delay(1);

            try
            {
                string commandOut = await _cmd.GetCommandOutputAsync($"ibmcloud sl file snapshot-list {fileVolume.Id}");
                string[] foundVolumes = commandOut.Split("\n").Skip(1).ToArray();

                decimal max = 0;
                foreach (string volume in foundVolumes)
                {
                    try
                    {
                        decimal bytes = decimal.Parse(volume.Split("   ")[3]);
                        if (bytes / 1073741824 > max) max = bytes / 1073741824;
                    }
                    catch {}
                }

                fileVolume.SnapshotMaxSize = (int) Math.Ceiling(max);
            }
            catch { }
        }

        private static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search, StringComparison.Ordinal);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        private static void OutputErrors()
        {
            Console.WriteLine();

            ConsoleColor before = Console.ForegroundColor;

            if (errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (Volume volume in errors)
                {
                    Console.WriteLine($"An error occurred for {volume.Type} volume with ID {volume} - Please check this manually.");
                }
                
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("No errors occurred");
            }

            Console.ForegroundColor = before;
        }

        private static void Save(List<Volume> volumes)
        {
            string csv = volumes.Aggregate("Id, Type, Username, Capacity GB, Endurance Tier per IOPS, Datacenter, Largest snapshot size GB\n", 
                (current, volume) => current + $"{volume.Id}, {volume.Type}, {volume.Username}, {volume.Capacity}, {volume.EnduranceTierPerIops}, {volume.Datacenter}, {volume.SnapshotMaxSize}\n");

            Console.WriteLine();
            Console.WriteLine("Enter a filename for the csv output file");
            Console.Write("> ");
            string filename = Console.ReadLine();

            while (true)
            {
                try
                {
                    File.WriteAllText($"{filename}.csv", csv);
                    Process.Start("explorer.exe", @$"/select,""{Directory.GetCurrentDirectory()}\{filename}.csv""");
                    break;
                }
                catch
                {
                    Console.WriteLine($"{filename}.csv could not be saved.\n");
                    Console.WriteLine("Enter a filename for the csv output file");
                    Console.Write("> ");
                    filename = Console.ReadLine();
                }
            }
        }
    }
}
