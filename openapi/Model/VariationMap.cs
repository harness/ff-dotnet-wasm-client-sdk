/*
 * Harness feature flag service client apis
 *
 * No description provided (generated by Openapi Generator https://github.com/openapitools/openapi-generator)
 *
 * The version of the OpenAPI document: 1.0.0
 * Contact: cf@harness.io
 * Generated by: https://github.com/openapitools/openapi-generator.git
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using FileParameter = io.harness.ff_dotnet_client_sdk.openapi.Client.FileParameter;
using OpenAPIDateConverter = io.harness.ff_dotnet_client_sdk.openapi.Client.OpenAPIDateConverter;

namespace io.harness.ff_dotnet_client_sdk.openapi.Model
{
    /// <summary>
    /// A mapping of variations to targets and target groups (segments).  The targets listed here should receive this variation.
    /// </summary>
    [DataContract(Name = "VariationMap")]
    internal partial class VariationMap : IEquatable<VariationMap>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VariationMap" /> class.
        /// </summary>
        [JsonConstructorAttribute]
        protected VariationMap() { }
        /// <summary>
        /// Initializes a new instance of the <see cref="VariationMap" /> class.
        /// </summary>
        /// <param name="variation">The variation identifier (required).</param>
        /// <param name="targets">A list of target mappings.</param>
        /// <param name="targetSegments">A list of target groups (segments).</param>
        public VariationMap(string variation = default(string), List<TargetMap> targets = default(List<TargetMap>), List<string> targetSegments = default(List<string>))
        {
            // to ensure "variation" is required (not null)
            if (variation == null)
            {
                throw new ArgumentNullException("variation is a required property for VariationMap and cannot be null");
            }
            this.Variation = variation;
            this.Targets = targets;
            this.TargetSegments = targetSegments;
        }

        /// <summary>
        /// The variation identifier
        /// </summary>
        /// <value>The variation identifier</value>
        /// <example>off-variation</example>
        [DataMember(Name = "variation", IsRequired = true, EmitDefaultValue = true)]
        public string Variation { get; set; }

        /// <summary>
        /// A list of target mappings
        /// </summary>
        /// <value>A list of target mappings</value>
        [DataMember(Name = "targets", EmitDefaultValue = false)]
        public List<TargetMap> Targets { get; set; }

        /// <summary>
        /// A list of target groups (segments)
        /// </summary>
        /// <value>A list of target groups (segments)</value>
        [DataMember(Name = "targetSegments", EmitDefaultValue = false)]
        public List<string> TargetSegments { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("class VariationMap {\n");
            sb.Append("  Variation: ").Append(Variation).Append("\n");
            sb.Append("  Targets: ").Append(Targets).Append("\n");
            sb.Append("  TargetSegments: ").Append(TargetSegments).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="input">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object input)
        {
            return this.Equals(input as VariationMap);
        }

        /// <summary>
        /// Returns true if VariationMap instances are equal
        /// </summary>
        /// <param name="input">Instance of VariationMap to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(VariationMap input)
        {
            if (input == null)
            {
                return false;
            }
            return 
                (
                    this.Variation == input.Variation ||
                    (this.Variation != null &&
                    this.Variation.Equals(input.Variation))
                ) && 
                (
                    this.Targets == input.Targets ||
                    this.Targets != null &&
                    input.Targets != null &&
                    this.Targets.SequenceEqual(input.Targets)
                ) && 
                (
                    this.TargetSegments == input.TargetSegments ||
                    this.TargetSegments != null &&
                    input.TargetSegments != null &&
                    this.TargetSegments.SequenceEqual(input.TargetSegments)
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hashCode = 41;
                if (this.Variation != null)
                {
                    hashCode = (hashCode * 59) + this.Variation.GetHashCode();
                }
                if (this.Targets != null)
                {
                    hashCode = (hashCode * 59) + this.Targets.GetHashCode();
                }
                if (this.TargetSegments != null)
                {
                    hashCode = (hashCode * 59) + this.TargetSegments.GetHashCode();
                }
                return hashCode;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            yield break;
        }
    }

}
