using System;
using System.IO;

using Synapse.Core.Utilities;


namespace Synapse.Services
{
    /// <summary>
    /// Hold the startup config for Synapse.Node; written as an independent class (not using .NET config) for cross-platform compatibility.
    /// </summary>
    public class SynapseNodeConfig
    {
        public SynapseNodeConfig()
        {
        }

        public static readonly string CurrentPath = $"{Path.GetDirectoryName( typeof( SynapseNodeConfig ).Assembly.Location )}";
        public static readonly string FileName = $"{Path.GetDirectoryName( typeof( SynapseNodeConfig ).Assembly.Location )}\\Synapse.Node.config.yaml";

        public int MaxServerThreads { get; set; } = 10;
        public string AuditLogRootPath { get; set; } = @".\Logs";
        public string ServiceLogRootPath { get; set; } = @".\Logs";
        public string Log4NetConversionPattern { get; set; } = "%d{ISO8601}|%-5p|(%t)|%m%n";
        public bool SerializeResultPlan { get; set; } = true;
        public bool ValidatePlanSignature { get; set; } = true;
        public string ControllerServiceUrl { get; set; } = "http://localhost:8008/synapse/execute";
        public string WebApiPort { get; set; } = "8000";

        public string GetResolvedAuditLogRootPath()
        {
            if( Path.IsPathRooted( AuditLogRootPath ) )
                return AuditLogRootPath;
            else
                return PathCombine( CurrentPath, AuditLogRootPath );
        }

        public string GetResolvedServiceLogRootPath()
        {
            if( Path.IsPathRooted( ServiceLogRootPath ) )
                return ServiceLogRootPath;
            else
                return PathCombine( CurrentPath, ServiceLogRootPath );
        }

        /// <summary>
        /// A wrapper on Path.Combine to correct for fronting/trailing backslashes that otherwise fail in Path.Combine.
        /// </summary>
        /// <param name="paths">An array of parts of the path.</param>
        /// <returns>The combined path</returns>
        public static string PathCombine(params string[] paths)
        {
            if( paths.Length > 0 )
            {
                int last = paths.Length - 1;
                for( int c = 0; c <= last; c++ )
                {
                    if( c != 0 )
                    {
                        paths[c] = paths[c].Trim( Path.DirectorySeparatorChar );
                    }
                    if( c != last )
                    {
                        paths[c] = string.Format( "{0}\\", paths[c] );
                    }
                }
            }
            else
            {
                return string.Empty;
            }

            return Path.Combine( paths );
        }


        public void Serialize()
        {
            YamlHelpers.SerializeFile( FileName, this, serializeAsJson: false, emitDefaultValues: true );
        }

        public static SynapseNodeConfig Deserialze()
        {
            if( !File.Exists( FileName ) )
                new SynapseNodeConfig().Serialize();

            return YamlHelpers.DeserializeFile<SynapseNodeConfig>( FileName );
        }
    }
}