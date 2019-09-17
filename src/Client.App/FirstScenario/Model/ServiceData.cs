using System;
using System.Linq;

namespace Client.App.FirstScenario.Model
{
    public class ServiceData : IEquatable<ServiceData>
    {
        public ServiceCodePackage[] CodePackages { get; set; }
        public string ProvisioningState { get; set; }
        public string HealthState { get; set; }
        public string Status { get; set; }

        public bool Equals(ServiceData other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return (CodePackages == null && other.CodePackages == null || (CodePackages != null && other.CodePackages != null) && CodePackages.SequenceEqual(other.CodePackages))
                   && ProvisioningState == other.ProvisioningState && HealthState == other.HealthState && Status == other.Status;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ServiceData) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (CodePackages != null ? CodePackages.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ProvisioningState != null ? ProvisioningState.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (HealthState != null ? HealthState.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Status != null ? Status.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}