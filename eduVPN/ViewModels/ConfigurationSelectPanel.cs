﻿/*
    eduVPN - End-user friendly VPN

    Copyright: 2017, The Commons Conservancy eduVPN Programme
    SPDX-License-Identifier: GPL-3.0+
*/

using Prism.Commands;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace eduVPN.ViewModels
{
    /// <summary>
    /// Locally authenticated configuration history panel
    /// </summary>
    public class ConfigurationSelectPanel : ConfigurationSelectBasePanel
    {
        #region Properties

        /// <summary>
        /// Currently selected configuration
        /// </summary>
        public Models.VPNConfiguration SelectedConfiguration
        {
            get { return _selected_configuration; }
            set { SetProperty(ref _selected_configuration, value); }
        }
        private Models.VPNConfiguration _selected_configuration;

        /// <summary>
        /// Connect selected configuration command
        /// </summary>
        public DelegateCommand ConnectSelectedConfiguration
        {
            get
            {
                if (_connect_selected_configuration == null)
                {
                    _connect_selected_configuration = new DelegateCommand(
                        // execute
                        async () =>
                        {
                            Parent.ChangeTaskCount(+1);
                            try
                            {
                                // Trigger initial authorization request.
                                await Parent.TriggerAuthorizationAsync(SelectedConfiguration.AuthenticatingInstance);

                                // Start VPN session.
                                var param = new ConnectWizard.StartSessionParams(
                                    InstanceSourceType,
                                    SelectedConfiguration.AuthenticatingInstance,
                                    SelectedConfiguration.ConnectingInstance,
                                    SelectedConfiguration.ConnectingProfile);
                                if (Parent.StartSession.CanExecute(param))
                                    Parent.StartSession.Execute(param);
                            }
                            catch (Exception ex) { Parent.Error = ex; }
                            finally { Parent.ChangeTaskCount(-1); }
                        },

                        // canExecute
                        () => SelectedConfiguration != null);

                    // Setup canExecute refreshing.
                    PropertyChanged += (object sender, PropertyChangedEventArgs e) => { if (e.PropertyName == nameof(SelectedConfiguration)) _connect_selected_configuration.RaiseCanExecuteChanged(); };
                }

                return _connect_selected_configuration;
            }
        }
        private DelegateCommand _connect_selected_configuration;

        /// <summary>
        /// Forget selected configuration command
        /// </summary>
        public DelegateCommand ForgetSelectedConfiguration
        {
            get
            {
                if (_forget_selected_configuration == null)
                {
                    _forget_selected_configuration = new DelegateCommand(
                        // execute
                        () =>
                        {
                            Parent.ChangeTaskCount(+1);
                            try
                            {
                                // Remove configuration from history.
                                ConfigurationHistory.Remove(SelectedConfiguration);

                                // Return to starting page. Should the abscence of configurations from history resolve in different starting page of course.
                                if (Parent.StartingPage != Parent.CurrentPage)
                                    Parent.CurrentPage = Parent.StartingPage;
                            }
                            catch (Exception ex) { Parent.Error = ex; }
                            finally { Parent.ChangeTaskCount(-1); }
                        },

                        // canExecute
                        () =>
                            SelectedConfiguration != null &&
                            ConfigurationHistory.IndexOf(SelectedConfiguration) >= 0 &&
                            !Parent.Sessions.Any(session =>
                                session.AuthenticatingInstance.Equals(SelectedConfiguration.AuthenticatingInstance) &&
                                session.ConnectingInstance.Equals(SelectedConfiguration.ConnectingInstance) &&
                                session.ConnectingProfile.Equals(SelectedConfiguration.ConnectingProfile)));

                    // Setup canExecute refreshing.
                    PropertyChanged += (object sender, PropertyChangedEventArgs e) => { if (e.PropertyName == nameof(SelectedConfiguration)) _forget_selected_configuration.RaiseCanExecuteChanged(); };
                    ConfigurationHistory.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) => _forget_selected_configuration.RaiseCanExecuteChanged();
                    Parent.Sessions.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) => _forget_selected_configuration.RaiseCanExecuteChanged();
                }

                return _forget_selected_configuration;
            }
        }
        private DelegateCommand _forget_selected_configuration;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs history panel
        /// </summary>
        /// <param name="parent">The page parent</param>
        /// <param name="instance_source_type">Instance source type</param>
        public ConfigurationSelectPanel(ConnectWizard parent, Models.InstanceSourceType instance_source_type) :
            base(parent, instance_source_type)
        {
        }

        #endregion
    }
}
