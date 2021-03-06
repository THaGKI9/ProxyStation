using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ProxyStation.Model;
using ProxyStation.ServerFilter;
using ProxyStation.Util;
using YamlDotNet.RepresentationModel;

namespace ProxyStation
{
    public static class ProfileFactory
    {
        static IEnvironmentManager environmentManager;

        public static void SetEnvironmentManager(IEnvironmentManager environmentManager)
        {
            ProfileFactory.environmentManager = environmentManager;
        }

        public static ProfileType ParseProfileTypeName(string profileType)
        {
            return (profileType.ToLower()) switch
            {
                "general" => ProfileType.General,
                "surge" => ProfileType.Surge,
                "clash" => ProfileType.Clash,
                "clash-proxy-provider" => ProfileType.ClashProxyProvider,
                "surge-list" => ProfileType.SurgeList,
                "alias" => ProfileType.Alias,
                _ => ProfileType.None,
            };
        }

        public static Profile Get(string profileName, ILogger logger)
        {
            var data = ProfileFactory.environmentManager.Get(profileName);
            if (String.IsNullOrEmpty(data)) return null;

            var profile = new Profile();
            using (var reader = new StringReader(data))
            {
                var yaml = new YamlStream();
                yaml.Load(reader);
                var mapping = (yaml.Documents[0].RootNode as YamlMappingNode).Children;

                profile.Source = (mapping["source"] as YamlScalarNode).Value;
                profile.Name = (mapping["name"] as YamlScalarNode).Value;
                profile.Type = ParseProfileTypeName((mapping["type"] as YamlScalarNode).Value);
                profile.AllowDirectAccess = Yaml.GetTruthFromYamlChildrenNode(yaml.Documents[0].RootNode, "allowDirectAccess");

                YamlNode filtersNode;
                if (!mapping.TryGetValue("filters", out filtersNode) || filtersNode.NodeType != YamlNodeType.Sequence)
                {
                    goto EndReading;
                }

                foreach (var filterNode in (filtersNode as YamlSequenceNode).Children)
                {
                    if (filterNode.NodeType != YamlNodeType.Mapping) continue;
                    var filter = FilterFactory.GetFilter(Yaml.GetStringOrDefaultFromYamlChildrenNode(filterNode, "name"));
                    if (filter == null) continue;
                    filter.LoadOptions(filterNode, logger);
                    profile.Filters.Add(filter);
                }
            }

        EndReading:
            return profile;
        }
    }
}