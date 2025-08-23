using System;

namespace PhantomProcessCatcher.data
{
    public class DllData : IEquatable<DllData>
    {
        public string Name { get; }
        public string Path { get; }

        public DllData(string name, string path)
        {
            this.Name = name;
            this.Path = path;
        }
        public bool Equals(DllData other)
        {
            return this.Name.Equals(other.Name);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int p1 = 13;
                int p2 = 17;
                int h = p1;
                h = h * p2 + Name.GetHashCode();
                return h;
            }
        }
    }
}
