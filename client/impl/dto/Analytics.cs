using ff_dotnet_wasm_client_sdk.client.dto;
using io.harness.ff_dotnet_client_sdk.openapi.Model;

namespace ff_dotnet_wasm_client_sdk.client.impl.dto
{
    internal class Analytics : IEquatable<Analytics>
    {

        internal FfTarget Target { get; }
        internal string EvaluationId { get; }
        internal Variation Variation { get; }

        internal Analytics(
            FfTarget target,
            string evaluationId,
            Variation variation
        )
        {
            Target = target;
            EvaluationId = evaluationId;
            Variation = variation;
        }

        public bool Equals(Analytics? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Target.Identifier.Equals(other.Target.Identifier) && EvaluationId == other.EvaluationId;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Analytics)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Target.Identifier.GetHashCode() * 397) ^ EvaluationId.GetHashCode();
            }
        }
    }
}