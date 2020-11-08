using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorNxmApp.Models
{
    public class JobUpdateModel
    {
        public int Id { get; set; }

        [Required]
        public string JobName { get; set; }
        public string BasePath { get; set; }

    }
}
