using System;
using System.Collections.Generic;
using System.Text;

namespace VolumeDetail
{
    internal class Volume
    {
        public Volume(int id, string type)
        {
            Id = id;
            Type = type;
        }

        public int Id { get; set; }
        public string Type { get; set; }
        public string Username { get; set; }
        public decimal Capacity { get; set; }
        public decimal EnduranceTierPerIops { get; set; }
        public string Datacenter { get; set; }
        public int SnapshotMaxSize { get; set; }
    }
}
