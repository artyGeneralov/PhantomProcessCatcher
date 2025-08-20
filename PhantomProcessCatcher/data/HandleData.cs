
using System;
using System.ComponentModel;

namespace PhantomProcessCatcher.data
{
    public class HandleData : IEquatable<HandleData>
    {



        [Browsable(false)]
        public int Pid { get; }
        [Browsable(false)]
        public ulong HandleAddress { get; }

        // To show:
        public string TypeName { get; } 
        public string HandleName { get; }
        public HandleData(int pid, ulong handleAddress, string typeName, string HandleName)
        {
            this.Pid = pid;
            this.HandleAddress = handleAddress;
            this.TypeName = typeName;
            this.HandleName = HandleName;
        }

        public bool Equals(HandleData other)
        {
            return Pid == other.Pid && HandleAddress == other.HandleAddress && HandleName.Equals(other.HandleName);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int p1 = 13;
                int p2 = 17;
                int h = p1;
                h = h * p2 + Pid;
                h = h * p2 + HandleAddress.GetHashCode();
                h = h * p2 + HandleName.GetHashCode();
                return h;
            }
        

        }
    }
}
