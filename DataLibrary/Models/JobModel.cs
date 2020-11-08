using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DataLibrary.Models
{
    public class JobModel
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }
        public string BasePath { get; set; }
        public int MaxElements { get; set; }
        public int BlockSize { get; set; }
        public int RotationTypeId { get; set; }
        public string Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string Interval { get; set; }
        public bool IsRunning { get; set; }
        public bool Deleted { get; set; }
    }
}
