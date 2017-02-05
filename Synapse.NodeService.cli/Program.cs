﻿using System;
using System.Collections.Generic;
using System.Text;


namespace Synapse.Services.NodeService.Cli
{
    class Program : Synapse.Common.CmdLine.HttpApiCliBase
    {
        static void Main(string[] args)
        {
            if( args.Length > 0 && (args[0].ToLower() == "interactive" || args[0].ToLower() == "i") )
            {
                Program p = new Program()
                {
                    IsInteractive = true,
                };
                if( args.Length > 1 )
                {
                    string[] s = args[1].Split( new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries );
                    if( s.Length == 2 )
                        p.BaseUrl = s[1];
                }

                string input = Console.ReadLine();
                while( input.ToLower() != "exit" )
                {
                    p.ProcessArgs( input.Split( ' ' ) );
                    input = Console.ReadLine();
                }
            }
            else
            {
                new Program().ProcessArgs( args );
            }
        }


        Dictionary<string, string> _methods = new Dictionary<string, string>();
        string _service = "service";

        public Program()
        {
            _methods.Add( "hello", "Hello" );
            _methods.Add( "hi", "Hello" );
            _methods.Add( "whoami", "WhoAmI" );
            _methods.Add( "who", "WhoAmI" );
            _methods.Add( "start", "StartPlanFile" );
            _methods.Add( "s", "StartPlanFile" );
            _methods.Add( "cancel", "CancelPlan" );
            _methods.Add( "c", "CancelPlan" );
            _methods.Add( "drainstop", "Drainstop" );
            _methods.Add( "dst", "Drainstop" );
            _methods.Add( "undrainstop", "Undrainstop" );
            _methods.Add( "ust", "Undrainstop" );
            _methods.Add( "DrainStatus", "GetIsDrainstopComplete" );
            _methods.Add( "dss", "GetIsDrainstopComplete" );
            _methods.Add( "QueueDepth", "GetCurrentQueueDepth" );
            _methods.Add( "qd", "GetCurrentQueueDepth" );
            _methods.Add( "QueueItems", "GetCurrentQueueItems" );
            _methods.Add( "qi", "GetCurrentQueueItems" );

            SynapseNodeConfig config = SynapseNodeConfig.Deserialze();
            BaseUrl = $"http://localhost:{config.WebApiPort}/synapse/node";
        }

        public bool IsInteractive { get; set; }
        public string BaseUrl { get; set; }

        void ProcessArgs(string[] args)
        {
            if( args.Length == 0 )
            {
                WriteHelpAndExit();
            }
            else
            {
                string arg0 = args[0].ToLower();

                if( _methods.ContainsKey( arg0 ) )
                {
                    if( args.Length > 1 )
                    {
                        bool error = false;
                        Dictionary<string, string> parms = ParseCmdLine( args, 1, ref error, suppressErrorMessages: true );
                        if( parms.ContainsKey( "url" ) )
                            BaseUrl = parms["url"];
                    }
                    Console.WriteLine( $"Calling {_methods[arg0]} on {BaseUrl}" );
                    RunMethod( new NodeServiceHttpApiClient( BaseUrl ), _methods[arg0], args );
                }
                else if( arg0.StartsWith( _service ) )
                    RunServiceAction( args );
                else
                    WriteHelpAndExit( "Unknown action." );
            }
        }


        protected virtual void RunServiceAction(string[] args)
        {
            if( args.Length < 2 )
                WriteHelpAndExit( "Not enough arguments specified." );

            string option = args[1].ToLower();

            switch( option )
            {
                case "run":
                {
                    SynapseNodeService.RunConsole();
                    break;
                }
                case "install":
                {
                    string message = string.Empty;
                    bool error = false;
                    Dictionary<string, string> values = ParseCmdLine( args, 2, ref error, true );
                    if( !InstallUtility.InstallAndStartService( configValues: values, message: out message ) )
                    {
                        Console.WriteLine( message );
                        Environment.Exit( 1 );
                    }

                    break;
                }
                case "uninstall":
                {
                    string message = string.Empty;
                    if( !InstallUtility.StopAndUninstallService( out message ) )
                    {
                        Console.WriteLine( message );
                        Environment.Exit( 1 );
                    }

                    break;
                }
                default:
                {
                    WriteHelpAndExit( "Unknown service action." );
                    Environment.Exit( 1 );

                    break;
                }
            }
        }


        #region Help
        protected override void WriteHelpAndExit(string errorMessage = null)
        {
            Dictionary<string, string> cdf = SynapseNodeConfig.GetConfigDefaultValues();
            StringBuilder df = new StringBuilder();
            df.AppendFormat( "{0,-15}- Optional install args, use argname:value.  Defaults shown.\r\n", "" );
            foreach( string key in cdf.Keys )
                df.AppendLine( $"                 - {key}:{cdf[key]}" );
            df.AppendLine( $"                 - Run:true  (Optionally Starts the Windows Service)\r\n" );

            bool haveError = !string.IsNullOrWhiteSpace( errorMessage );

            ConsoleColor defaultColor = Console.ForegroundColor;

            Console_WriteLine( $"synapse.node.cli.exe, Version: {typeof( Program ).Assembly.GetName().Version}\r\n", ConsoleColor.Green );
            Console.WriteLine( "Syntax:" );
            Console_WriteLine( "  synapse.node.cli.exe service {0}command{1} | {0}httpAction parm:value{1} |", ConsoleColor.Cyan, "{", "}" );
            Console.WriteLine( "       interactive|i [url:http://{1}host:port{2}/synapse/node]\r\n", "", "{", "}" );
            Console_WriteLine( "  About URLs:{0,-2}URL is an optional parameter on all commands except 'service'", ConsoleColor.Green, "" );
            Console.WriteLine( "{0,-15}commands. Specify as [url:http://{1}host:port{2}/synapse/node].", "", "{", "}" );
            Console.WriteLine( "{0,-15}URL default is localhost:{1}port{2} (See WebApiPort in config.yaml)\r\n", "", "{", "}" );
            Console.WriteLine( "  interactive{0,-2}Run this CLI in interactive mode, optionally specify URL.", "" );
            Console.WriteLine( "{0,-15}All commands below work in standard or interactive modes.\r\n", "" );
            Console.WriteLine( "  service{0,-6}Install/Uninstall the Windows Service, or Run the Service", "" );
            Console.WriteLine( "{0,-15}as a cmdline-hosted daemon.", "" );
            Console.WriteLine( "{0,-15}- Commands: install|uninstall|run", "" );
            Console.WriteLine( "{0,-15}- Example:  synapse.node.cli service run", "" );
            Console.WriteLine( df.ToString() );
            Console.WriteLine( "  httpAction{0,-3}Execute a command, optionally specify URL.", "" );
            Console.WriteLine( "{0,-15}Parm help: synapse.node.cli {1}httpAction{2} help.\r\n", "", "{", "}" );
            Console.WriteLine( "  - httpActions:", "" );
            Console.WriteLine( "    - Hello|hi           Returns 'Hello, World!'.", "" );
            Console.WriteLine( "    - WhoAmI|who         Returns NodeServer User Context.", "" );
            Console.WriteLine( "    - Start|s            Start a new Plan Instance.", "" );
            Console.WriteLine( "    - Cancel|c           Cancel a Plan Instance.", "" );
            Console.WriteLine( "    - Drainstop|dst      Prevents the node from receiving incoming requests;", "" );
            Console.WriteLine( "                         allows existing threads to complete. Optionally stops", "" );
            Console.WriteLine( "                         the Service when queue is fully drained.", "" );
            Console.WriteLine( "    - DrainStatus|dss    Returns true/false on whether queue is fully drained.", "" );
            Console.WriteLine( "    - QueueDepth|qd      Returns the number of items remaining in the queue.", "" );
            Console.WriteLine( "    - QueueItems|qi      Returns the list of items remaining in the queue.", "" );
            Console.WriteLine( "    - Undrainstop|ust    Resumes normal request processing.\r\n", "" );
            Console.WriteLine( "  Examples:", "" );
            Console.WriteLine( "    synapse.node.cli hi url:http://somehost/synapse/node", "" );
            Console.WriteLine( "    synapse.node.cli s help", "" );
            Console.WriteLine( "    synapse.node.cli s planInstanceId:0 dryRun:false filePath:C:\\planFile.yaml", "" );
            Console.WriteLine( "    synapse.node.cli dst url:http://somehost/synapse/node", "" );
            Console.WriteLine( "    synapse.node.cli i url:http://somehost/synapse/node", "" );
            Console.WriteLine( "    synapse.node.cli i", "" );

            if( haveError )
                Console_WriteLine( $"\r\n\r\n*** Last error:\r\n{errorMessage}\r\n", ConsoleColor.Red );

            Console.ForegroundColor = defaultColor;

            if( !IsInteractive )
                Environment.Exit( haveError ? 1 : 0 );
        }
        #endregion
    }
}