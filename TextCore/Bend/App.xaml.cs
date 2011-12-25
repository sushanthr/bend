using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Deployment.Application;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Bend
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891")]

    interface ICLRRuntimeInfo
    {

        void xGetVersionString();
        void xGetRuntimeDirectory();
        void xIsLoaded();
        void xIsLoadable();
        void xLoadErrorString();
        void xLoadLibrary();
        void xGetProcAddress();
        void xGetInterface();
        void xSetDefaultStartupFlags();
        void xGetDefaultStartupFlags();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void BindAsLegacyV2Runtime();
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            if (PersistantStorage.StorageObject.ShowSplashScreen)
            {
                System.Windows.SplashScreen splashScreen = new System.Windows.SplashScreen("images/splash.png");
                splashScreen.Show(true);
            }
                        
            if (!ApplicationDeployment.IsNetworkDeployed)
            {
                bool debugApplication = true;
                // this application was started up from the exe that isnt good.
                // restart application through clickonce.
                string[] commandLineArgs = Environment.GetCommandLineArgs();
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
                    }
                }
                else
                {
                    argument = "";
                }

                if (!debugApplication)                
                {
                    string clickOnceApplication = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs\\Bend\\Bend\\Bend.appref-ms";
                    System.Diagnostics.Process.Start(clickOnceApplication, argument);
                    this.Shutdown();
                }
            }

            ICLRRuntimeInfo runtimeInfo = (ICLRRuntimeInfo)RuntimeEnvironment.GetRuntimeInterfaceAsObject(Guid.Empty, typeof(ICLRRuntimeInfo).GUID); 
            runtimeInfo.BindAsLegacyV2Runtime();
        }
    }
}
