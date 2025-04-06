using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Deployment.Application;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Console;

namespace Bend
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            bool debugApplication = false;
            // this application was started up from the exe that isnt good.
            // restart application through clickonce.
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            bool argumentIsFile = false;
            string argument;
            if (commandLineArgs.Length > 1)
            {
                if (commandLineArgs[1] == "/DEBUG")
                {
                    // To allow debugging continue to run this application.
                    debugApplication = true;
                    argument = "";
                }
                else
                {
                    if (System.IO.Path.IsPathRooted(commandLineArgs[1]))
                    {
                        argument = commandLineArgs[1];
                    }
                    else
                    {
                        argument = Environment.CurrentDirectory + "\\" + commandLineArgs[1];
                    }
                    argumentIsFile = System.IO.File.Exists(argument);
                }
            }
            else
            {
                argument = "";
            }

            if (!argumentIsFile &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData != null &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData.Length > 0)
            {
                argument = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData[0];
                argumentIsFile = System.IO.File.Exists(argument);
            }

            IntPtr hwnd;
            if (argumentIsFile && InterBendCommunication.FindOtherApplicationInstance(out hwnd))
            {
                // There is another instance of bend running somewhere, send this file to it.
                InterBendCommunication.SendFileNameToHwnd(hwnd, argument);
                this.Shutdown();
            }
            else if (!ApplicationDeployment.IsNetworkDeployed && !debugApplication)
            {
                LaunchBendClickOnceApplication(argument);
                this.Shutdown();
            }

            TermPTYProxy.EnsureServerRunning();
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            StyledMessageBox.Show( "Unhandled Exception" , sender.ToString() + e.ToString() + "\n" + e.Exception.StackTrace);
        }

        internal static void LaunchBendClickOnceApplication(string argument)
        {
            string clickOnceApplication = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs\\Bend\\Bend\\Bend.appref-ms";
            System.Diagnostics.Process.Start(clickOnceApplication, argument);
        }
    }
}
