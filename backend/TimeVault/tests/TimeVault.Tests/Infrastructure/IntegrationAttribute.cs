using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TimeVault.Tests.Infrastructure
{
    /// <summary>
    /// Attribute used to mark integration tests so they can be filtered out if needed
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    [TraitDiscoverer("TimeVault.Tests.Infrastructure.IntegrationDiscoverer", "TimeVault.Tests")]
    public class IntegrationAttribute : Attribute, ITraitAttribute
    {
    }

    /// <summary>
    /// Discovers integration attributes and adds them as traits for xUnit
    /// </summary>
    public class IntegrationDiscoverer : ITraitDiscoverer
    {
        public const string Category = "Category";
        public const string Integration = "Integration";

        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            yield return new KeyValuePair<string, string>(Category, Integration);
        }
    }
} 