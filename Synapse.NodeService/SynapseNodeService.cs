﻿using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Threading;

using Synapse.Core.DataAccessLayer;
using Synapse.Services.Common;

using log4net;

namespace Synapse.Services
{
    public partial class SynapseNodeService : ServiceBase
    {
        //LogUtility _logUtil = new LogUtility();
        public static ILog Logger = LogManager.GetLogger( "SynapseNodeServer" );
        public static SynapseNodeConfig Config = null;

        ServiceHost _serviceHost = null;


        public SynapseNodeService()
        {
            Config = SynapseNodeConfig.Deserialze();

            InitializeComponent();
            this.ServiceName = "Synapse.Node";
        }

        //public void InitializeLogger()
        //{
        //    string logRootPath = System.IO.Directory.CreateDirectory( Config.ServiceLogRootPath ).FullName;
        //    string logFilePath = $"{logRootPath}\\Synapse.Node.log";
        //    _logUtil.InitDefaultLogger( "SynapseNodeServer", "SynapseNodeServer", logFilePath, Config.Log4NetConversionPattern, "DEBUG" );
        //    Logger = _logUtil._logger;
        //}

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

            InstallService( args );

#if DEBUG
            SynapseNodeService s = new SynapseNodeService();
            s.OnStart( null );
            Thread.Sleep( Timeout.Infinite );
            s.OnStop();
#else
			ServiceBase.Run( new SynapseNodeService() );
#endif
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
                        ok = InstallUtility.InstallService( install: true, message: out message );
                    else if( arg0 == "/uninstall" || arg0 == "/u" )
                        ok = InstallUtility.InstallService( install: false, message: out message );

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

        protected override void OnStart(string[] args)
        {
            try
            {
                Logger.Info( ServiceStatus.Starting );

                EnsureDatabase();

                if( _serviceHost != null )
                    _serviceHost.Close();

                SynapseNodeServer.InitPlanScheduler();

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
                Environment.Exit( 99 );
            }
        }

        protected override void OnStop()
        {
            Logger.Info( ServiceStatus.Stopping );
            if( _serviceHost != null )
                _serviceHost.Close();
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
            string source = "SynapseService";
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
            string msg = $"synapse.node.exe, Version: {typeof( SynapseNodeService ).Assembly.GetName().Version}\r\nSyntax:\r\n  synapse.node.exe /install | /uninstall";

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