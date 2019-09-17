using System;

namespace Client.App.FirstScenario.Model
{
    public class ApplicationServiceData : IEquatable<ApplicationServiceData>
    {
        public string HealthState { get; set; }

        public bool Equals(ApplicationServiceData other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return HealthState == other.HealthState;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ApplicationServiceData) obj);
        }

        public override int GetHashCode()
        {
            return (HealthState != null ? HealthState.GetHashCode() : 0);
        }
    }
}