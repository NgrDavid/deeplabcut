﻿using System;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Bonsai.DeepLabCut
{
    static class ConfigHelper
    {
        static YamlMappingNode OpenFile(string fileName)
        {
            var yaml = new YamlStream();
            var reader = new StringReader(File.ReadAllText(fileName));
            yaml.Load(reader);

            var document = yaml.Documents.FirstOrDefault();
            if (document == null)
            {
                throw new ArgumentException("The specified pose config file is empty.", nameof(fileName));
            }

            var mapping = document.RootNode as YamlMappingNode;
            return mapping;
        }

        public static PoseConfig LoadPoseConfig(string fileName)
        {
            var mapping = OpenFile(fileName);
            return LoadPoseConfig(mapping);
        }

        public static PoseConfig LoadPoseConfig(YamlMappingNode mapping)
        {
            var config = new PoseConfig();
            foreach (var entry in mapping.Children)
            {
                switch (((string)entry.Key).ToLowerInvariant())
                {
                    case "all_joints_names":
                        var jointNames = (YamlSequenceNode)entry.Value;
                        foreach (var part in jointNames.Children)
                        {
                            config.JointNames.Add((string)part);
                        }
                        break;
                    case "location_refinement":
                        config.LocationRefinement = bool.Parse((string)entry.Value);
                        break;
                    case "partaffinityfield_predict":
                        config.PredictPartAffinityField = bool.Parse((string)entry.Value);
                        break;
                    case "partaffinityfield_graph":
                        var partAffinityGraph = (YamlSequenceNode)entry.Value;
                        foreach (YamlSequenceNode link in partAffinityGraph)
                        {
                            PoseConfig.Link linkDescriptor;
                            linkDescriptor.Source = int.Parse((string)link.Children[0]);
                            linkDescriptor.Target = int.Parse((string)link.Children[1]);
                            config.PartAffinityGraph.Add(linkDescriptor);
                        }
                        break;
                }
            }
            return config;
        }
    }
}
