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
    /// TBD
    /// </summary>
    [DataContract(Name = "ProxyConfig")]
    internal partial class ProxyConfig : IEquatable<ProxyConfig>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyConfig" /> class.
        /// </summary>
        [JsonConstructorAttribute]
        protected ProxyConfig() { }
        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyConfig" /> class.
        /// </summary>
        /// <param name="varVersion">The version of this object.  The version will be incremented each time the object is modified.</param>
        /// <param name="pageCount">The total number of pages (required).</param>
        /// <param name="itemCount">The total number of items (required).</param>
        /// <param name="pageSize">The number of items per page (required).</param>
        /// <param name="pageIndex">The current page (required).</param>
        /// <param name="environments">environments.</param>
        public ProxyConfig(int varVersion = default(int), int pageCount = default(int), int itemCount = default(int), int pageSize = default(int), int pageIndex = default(int), List<ProxyConfigAllOfEnvironments> environments = default(List<ProxyConfigAllOfEnvironments>))
        {
            this.PageCount = pageCount;
            this.ItemCount = itemCount;
            this.PageSize = pageSize;
            this.PageIndex = pageIndex;
            this.VarVersion = varVersion;
            this.Environments = environments;
        }

        /// <summary>
        /// The version of this object.  The version will be incremented each time the object is modified
        /// </summary>
        /// <value>The version of this object.  The version will be incremented each time the object is modified</value>
        /// <example>5</example>
        [DataMember(Name = "version", EmitDefaultValue = false)]
        public int VarVersion { get; set; }

        /// <summary>
        /// The total number of pages
        /// </summary>
        /// <value>The total number of pages</value>
        /// <example>100</example>
        [DataMember(Name = "pageCount", IsRequired = true, EmitDefaultValue = true)]
        public int PageCount { get; set; }

        /// <summary>
        /// The total number of items
        /// </summary>
        /// <value>The total number of items</value>
        /// <example>1</example>
        [DataMember(Name = "itemCount", IsRequired = true, EmitDefaultValue = true)]
        public int ItemCount { get; set; }

        /// <summary>
        /// The number of items per page
        /// </summary>
        /// <value>The number of items per page</value>
        /// <example>1</example>
        [DataMember(Name = "pageSize", IsRequired = true, EmitDefaultValue = true)]
        public int PageSize { get; set; }

        /// <summary>
        /// The current page
        /// </summary>
        /// <value>The current page</value>
        /// <example>0</example>
        [DataMember(Name = "pageIndex", IsRequired = true, EmitDefaultValue = true)]
        public int PageIndex { get; set; }

        /// <summary>
        /// Gets or Sets Environments
        /// </summary>
        [DataMember(Name = "environments", EmitDefaultValue = false)]
        public List<ProxyConfigAllOfEnvironments> Environments { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("class ProxyConfig {\n");
            sb.Append("  VarVersion: ").Append(VarVersion).Append("\n");
            sb.Append("  PageCount: ").Append(PageCount).Append("\n");
            sb.Append("  ItemCount: ").Append(ItemCount).Append("\n");
            sb.Append("  PageSize: ").Append(PageSize).Append("\n");
            sb.Append("  PageIndex: ").Append(PageIndex).Append("\n");
            sb.Append("  Environments: ").Append(Environments).Append("\n");
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
            return this.Equals(input as ProxyConfig);
        }

        /// <summary>
        /// Returns true if ProxyConfig instances are equal
        /// </summary>
        /// <param name="input">Instance of ProxyConfig to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(ProxyConfig input)
        {
            if (input == null)
            {
                return false;
            }
            return 
                (
                    this.VarVersion == input.VarVersion ||
                    this.VarVersion.Equals(input.VarVersion)
                ) && 
                (
                    this.PageCount == input.PageCount ||
                    this.PageCount.Equals(input.PageCount)
                ) && 
                (
                    this.ItemCount == input.ItemCount ||
                    this.ItemCount.Equals(input.ItemCount)
                ) && 
                (
                    this.PageSize == input.PageSize ||
                    this.PageSize.Equals(input.PageSize)
                ) && 
                (
                    this.PageIndex == input.PageIndex ||
                    this.PageIndex.Equals(input.PageIndex)
                ) && 
                (
                    this.Environments == input.Environments ||
                    this.Environments != null &&
                    input.Environments != null &&
                    this.Environments.SequenceEqual(input.Environments)
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
                hashCode = (hashCode * 59) + this.VarVersion.GetHashCode();
                hashCode = (hashCode * 59) + this.PageCount.GetHashCode();
                hashCode = (hashCode * 59) + this.ItemCount.GetHashCode();
                hashCode = (hashCode * 59) + this.PageSize.GetHashCode();
                hashCode = (hashCode * 59) + this.PageIndex.GetHashCode();
                if (this.Environments != null)
                {
                    hashCode = (hashCode * 59) + this.Environments.GetHashCode();
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