﻿/*
    eduVPN - End-user friendly VPN

    Copyright: 2017, The Commons Conservancy eduVPN Programme
    SPDX-License-Identifier: GPL-3.0+
*/

using eduVPN;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace eduVPNClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Token used to abort unfinished background processes in case of application shutdown.
        /// </summary>
        private static CancellationTokenSource _abort = new CancellationTokenSource();

        /// <summary>
        /// Constructs the application
        /// </summary>
        public App()
        {
            // .NET 3.5 allows Schannel to use SSL 3 and TLS 1.0 by default. Instead of hacking user computer's registry, extend it in runtime.
            // System.Net.SecurityProtocolType lacks appropriate constants prior to .NET 4.5.
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)0x0C00;

            // Launch instance list load in the background.
            ThreadPool.QueueUserWorkItem(new WaitCallback(GetInstanceList), this);

            // Register ShutdownStarted callback.
            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
        }

        /// <summary>
        /// Loads instance list from web service.
        /// </summary>
        /// <param name="sender">Sender object</param>
        private static void GetInstanceList(object sender)
        {
            var app = sender as App;

            try
            {
                // Load instance list.
                var obj = InstanceList.Get(
                    new Uri(eduVPNClient.Properties.Settings.Default.InstanceDirectory),
                    Convert.FromBase64String(eduVPNClient.Properties.Settings.Default.InstanceDirectoryPubKey),
                    _abort.Token);

                // Notify the sender the instance list was loaded.
                app.Dispatcher.Invoke(DispatcherPriority.Normal,
                    new SetInstanceListDelegate(app.SetInstanceList),
                    obj);
            }
            catch (OperationCanceledException)
            {
                // The load was aborted.
            }
            catch (Exception ex)
            {
                // Notify the sender the instance list loading failed.
                app.Dispatcher.Invoke(DispatcherPriority.Normal,
                    new SetInstanceListFailedDelegate(app.SetInstanceListFailed),
                    ex);
            }
        }

        private delegate void SetInstanceListDelegate(Dictionary<string, object> obj);
        private void SetInstanceList(Dictionary<string, object> obj)
        {
            // Append "Other instance" entry to JSON data.
            eduJSON.Parser.GetValue<List<object>>(obj, "instances").Add(new Dictionary<string, object>
                {
                    { "base_uri", "nl.eduvpn.app.windows:other" },
                    { "display_name", (string)FindResource("OtherInstance") }
                });

            // Load instances.
            ((InstanceList)FindResource("InstanceList")).Load(obj);
        }

        private delegate void SetInstanceListFailedDelegate(Exception ex);
        private void SetInstanceListFailed(Exception ex)
        {
            MessageBox.Show(
                MainWindow,
                String.Format((string)FindResource("ErrorInstanceListLoadingFailed"), ex.Message),
                (string)FindResource("MessageBoxTitleWarning"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void Dispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            // Raise the abort flag to gracefully shutdown all background threads.
            _abort.Cancel();
        }
    }
}