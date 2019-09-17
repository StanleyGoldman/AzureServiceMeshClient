using System;

namespace Client.App.FirstScenario.Model
{
    public class ServiceCodePackage : IEquatable<ServiceCodePackage>
    {
        public string Name { get; set; }

        public bool Equals(ServiceCodePackage other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ServiceCodePackage) obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }
}