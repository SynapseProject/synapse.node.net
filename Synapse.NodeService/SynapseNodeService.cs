using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using log4net;

using Synapse.Common.CmdLine;
using Synapse.Core.DataAccessLayer;
using Synapse.Services.Common;

namespace Synapse.Services
{
    public partial class SynapseNodeService : ServiceBase
    {
        public static ILog Logger = LogManager.GetLogger( "SynapseNodeServer" );
        public static SynapseNodeConfig Config = null;

        ServiceHost _serviceHost = null;


        public SynapseNodeService()
        {
            Config = SynapseNodeConfig.Deserialze();

            InitializeComponent();

            this.ServiceName = Config.ServiceName;
        }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

#if DEBUG
            RunConsole();
#endif

            InstallService( args ); //only runs RELEASE
            RunService(); //only runs RELEASE
        }

        /// <summary>
        /// Install/Uninstall the service.
        /// Only works for Release build, as Debug will timeout on service start anyway (Thread.Sleep( Timeout.Infinite );).
        /// </summary>
        /// <param name="args"></param>
        [Conditional( "RELEASE" )]
        static void InstallService(string[] args)
        {
            if( Environment.UserInteractive )
                if( args.Length > 0 )
                {
                    bool ok = false;
                    string message = string.Empty;

                    string arg0 = args[0].ToLower();
                    if( arg0 == "/install" || arg0 == "/i" )
                    {
                        bool error = false;
                        Dictionary<string, string> values = CmdLineUtilities.ParseCmdLine( args, 1, ref error, ref message, null );
                        if( !error )
                            ok = InstallUtility.InstallAndStartService( configValues: values, message: out message );
                    }
                    else if( arg0 == "/uninstall" || arg0 == "/u" )
                    {
                        ok = InstallUtility.StopAndUninstallService( out message );
                    }

                    if( !ok )
                        WriteHelpAndExit( message );
                    else
                        Environment.Exit( 0 );
                }
                else
                {
                    WriteHelpAndExit();
                }
        }

        [Conditional( "RELEASE" )]
        public static void RunService()
        {
            ServiceBase.Run( new SynapseNodeService() );
        }

        public static void RunConsole()
        {
            ConsoleColor current = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine( "Starting Synapse.Node: Press Ctrl-C/Ctrl-Break to stop." );
            Console.ForegroundColor = current;

            using( SynapseNodeService s = new SynapseNodeService() )
            {
                s.OnStart( null );
                Thread.Sleep( Timeout.Infinite );
                s.OnStop();
            }
            Console.WriteLine( "Terminating Synapse.Node." );
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                Logger.Info( ServiceStatus.Starting );

                EnsureDatabase();

                if( _serviceHost != null )
                    _serviceHost.Close();

                SynapseNodeServer.InitPlanScheduler();
                SynapseNodeServer.DrainstopCallback = () => StopCallback();

                _serviceHost = new ServiceHost( typeof( SynapseNodeServer ) );
                _serviceHost.Open();

                Logger.Info( ServiceStatus.Running );
            }
            catch( Exception ex )
            {
                string msg = ex.Message;
                if( ex.HResult == -2146233052 )
                    msg += "  Ensure the x86/x64 Sqlite folders are included with the distribution.";

                //_log.Write( Synapse.Common.LogLevel.Fatal, msg );
                Logger.Fatal( msg );
                WriteEventLog( msg );

                this.Stop();
                Environment.Exit( 1 );
            }
        }

        void StopCallback()
        {
            this.Stop();
            Environment.Exit( 0 );
        }

        protected override void OnStop()
        {
            Logger.Info( ServiceStatus.Stopping );

            try
            {
                if( _serviceHost != null )
                    _serviceHost.Close();
            }
            catch( Exception ex )
            {
                Logger.Fatal( ex.Message );
                WriteEventLog( ex.Message );
            }

            Logger.Info( ServiceStatus.Stopped );
        }


        #region ensure database exists
        void EnsureDatabase()
        {
            Logger.Info( "EnsureDatabase: Checking file exists and connection is valid." );
            SynapseDal.CreateDatabase();
            Exception testResult = null;
            string message = string.Empty;
            if( !SynapseDal.TestConnection( out testResult, out message ) )
                throw testResult;
            Logger.Info( $"EnsureDatabase: Success. {message}" );
        }
        #endregion


        #region exception handling
        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string source = "SynapseNodeService";
            string log = "Application";

            string msg = ((Exception)e.ExceptionObject).Message + ((Exception)e.ExceptionObject).InnerException.Message;

            Logger.Error( ((Exception)e.ExceptionObject).Message );
            Logger.Error( msg );

            try
            {
                if( !EventLog.SourceExists( source ) )
                    EventLog.CreateEventSource( source, log );

                EventLog.WriteEntry( source, msg, EventLogEntryType.Error );
            }
            catch { }

            try
            {
                string logRootPath = System.IO.Directory.CreateDirectory(
                    SynapseNodeService.Config.GetResolvedServiceLogRootPath() ).FullName;
                string logFilePath = $"{logRootPath}\\UnhandledException_{DateTime.Now.Ticks}.log";
                Exception ex = (Exception)e.ExceptionObject;
                string innerMsg = ex.InnerException != null ? ex.InnerException.Message : string.Empty;
                System.IO.File.AppendAllText( logFilePath, $"{ex.Message}\r\n\r\n{innerMsg}" );
            }
            catch { }
        }

        void WriteEventLog(string msg, EventLogEntryType entryType = EventLogEntryType.Error)
        {
            string source = "SynapseNodeService";
            string log = "Application";

            try
            {
                if( !EventLog.SourceExists( source ) )
                    EventLog.CreateEventSource( source, log );

                EventLog.WriteEntry( source, msg, entryType );
            }
            catch { }
        }
        #endregion

        #region Help
        static void WriteHelpAndExit(string errorMessage = null)
        {
            bool haveError = !string.IsNullOrWhiteSpace( errorMessage );

            MessageBoxIcon icon = MessageBoxIcon.Information;
            Dictionary<string, string> cdf = SynapseNodeConfig.GetConfigDefaultValues();
            StringBuilder df = new StringBuilder();
            df.AppendLine( $"Optional args for configuring /install, use argname:value.  Defaults shown." );
            foreach( string key in cdf.Keys )
                df.AppendLine( $" - {key}:{cdf[key]}" );
            df.AppendLine( $" - Run:true  (Optionally Starts the Windows Service)" );

            string msg = $"synapse.node.exe, Version: {typeof( SynapseNodeService ).Assembly.GetName().Version}\r\nSyntax:\r\n  synapse.node.exe /install | /uninstall\r\n\r\n{df.ToString()}";

            if( haveError )
            {
                msg += $"\r\n\r\n* Last error:\r\n{errorMessage}\r\nSee logs for details.";
                icon = MessageBoxIcon.Error;
            }

            MessageBox.Show( msg, "Synapse Node Service", MessageBoxButtons.OK, icon );

            Environment.Exit( haveError ? 1 : 0 );
        }
        #endregion
    }
}


//LogUtility _logUtil = new LogUtility();
//public void InitializeLogger()
//{
//    string logRootPath = System.IO.Directory.CreateDirectory( Config.ServiceLogRootPath ).FullName;
//    string logFilePath = $"{logRootPath}\\Synapse.Node.log";
//    _logUtil.InitDefaultLogger( "SynapseNodeServer", "SynapseNodeServer", logFilePath, Config.Log4NetConversionPattern, "DEBUG" );
//    Logger = _logUtil._logger;
//}
