using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class Job
    {
        public delegate void newEventDelegate(EventProperties properties);
        //private newEventDelegate newEventFunction;
        //private string name;

        //public newEventDelegate NewEventFunction { get => newEventFunction; set => newEventFunction = value; }

        //public Job(string name, newEventDelegate eventHandler)
        //{
        //    this.name = name;
        //    this.NewEventFunction = eventHandler;
        //}

    }
}
