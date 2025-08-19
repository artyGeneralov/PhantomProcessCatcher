

using System;

namespace PhantomProcessCatcher.data
{
    public class ProcessEntry
    {
        public int Pid { get; private set; }
        public string Name { get; private set; }
        public DateTime CreationTime { get; private set; }
        public string User { get; private set; }

        public ProcessEntry()
        {
            Pid = 0;
            Name = "Default";
            CreationTime = DateTime.Now;
            User = "Default";
        }
        public ProcessEntry(int  pid, string name, DateTime creationTime, string user)
        {
            this.CreationTime = creationTime;
            this.Pid = pid;
            this.Name = name;
            this.User = user;
        }

        public ProcessEntry(ProcessEntry e)
        {
            this.Pid = e.Pid;
            this.Name = e.Name;
            this.CreationTime = e.CreationTime;
            this.User = e.User;
        }

    }
}
