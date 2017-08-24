﻿/*
    eduVPN - End-user friendly VPN

    Copyright: 2017, The Commons Conservancy eduVPN Programme
    SPDX-License-Identifier: GPL-3.0+
*/

using eduOAuth;
using eduVPN.JSON;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace eduVPN.Models
{
    /// <summary>
    /// An eduVPN instance (VPN service provider) basic info
    /// </summary>
    public class InstanceInfo : BindableBase, JSON.ILoadableItem, IXmlSerializable
    {
        #region Fields

        /// <summary>
        /// Instance API endpoints
        /// </summary>
        private InstanceEndpoints _endpoints;

        /// <summary>
        /// OAuth pending authorization grant
        /// </summary>
        private AuthorizationGrant _authorization_grant;

        /// <summary>
        /// Registered client redirect callback URI (endpoint)
        /// </summary>
        public const string RedirectEndpoint = "org.eduvpn.app:/api/callback";

        /// <summary>
        /// List of available profiles
        /// </summary>
        private JSON.Collection<Models.ProfileInfo> _profile_list;

        /// <summary>
        /// Client certificate
        /// </summary>
        private X509Certificate2 _client_certificate;

        #endregion

        #region Properties

        /// <summary>
        /// Instance base URI
        /// </summary>
        public Uri Base
        {
            get { return _base; }
            set {
                if (value != _base)
                {
                    _base = value; RaisePropertyChanged();

                    // Setting the base also resets internal state (fields).
                    _endpoints = null;
                    _authorization_grant = null;
                    _profile_list = null;
                    _client_certificate = null;
                }
            }
        }
        private Uri _base;

        /// <summary>
        /// Instance name to display in GUI
        /// </summary>
        public string DisplayName
        {
            get { return _display_name; }
            set { if (value != _display_name) { _display_name = value; RaisePropertyChanged(); } }
        }
        private string _display_name;

        /// <summary>
        /// Instance logo URI
        /// </summary>
        public Uri Logo
        {
            get { return _logo; }
            set { if (value != _logo) { _logo = value; RaisePropertyChanged(); } }
        }
        private Uri _logo;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs the instance info
        /// </summary>
        public InstanceInfo() :
            base()
        {
        }

        /// <summary>
        /// Constructs the authenticating instance info for given federated instance source
        /// </summary>
        /// <param name="instance_source">Federated instance source</param>
        public InstanceInfo(FederatedInstanceSourceInfo instance_source) :
            this()
        {
            // Set API endpoints manually.
            _endpoints = new InstanceEndpoints()
            {
                AuthorizationEndpoint = instance_source.AuthorizationEndpoint,
                TokenEndpoint = instance_source.TokenEndpoint
            };
        }

        #endregion

        #region Methods

        public override string ToString()
        {
            return DisplayName;
        }

        /// <summary>
        /// Gets and loads instance endpoints
        /// </summary>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Instance endpoints</returns>
        public InstanceEndpoints GetEndpoints(CancellationToken ct = default(CancellationToken))
        {
            var task = GetEndpointsAsync(ct);
            try
            {
                task.Wait(ct);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Gets and loads instance endpoints asynchronously
        /// </summary>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Asynchronous operation with expected instance endpoints</returns>
        public async Task<InstanceEndpoints> GetEndpointsAsync(CancellationToken ct = default(CancellationToken))
        {
            if (_endpoints == null)
            {
                try
                {
                    // Get and load API endpoints.
                    _endpoints = new InstanceEndpoints();
                    var uri_builder = new UriBuilder(Base);
                    uri_builder.Path += "info.json";
                    _endpoints.LoadJSON((await JSON.Response.GetAsync(
                        uri: uri_builder.Uri,
                        ct: ct)).Value, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { throw new AggregateException(Resources.Strings.ErrorEndpointsLoad, ex); }
            }

            return _endpoints;
        }

        /// <summary>
        /// Triggers instance client authorization
        /// </summary>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        public void RequestAuthorization(CancellationToken ct = default(CancellationToken))
        {
            // Prepare new authorization grant.
            _authorization_grant = new AuthorizationGrant()
            {
                AuthorizationEndpoint = GetEndpoints(ct).AuthorizationEndpoint,
                RedirectEndpoint = new Uri(RedirectEndpoint),
                ClientID = "org.eduvpn.app",
                Scope = new List<string>() { "config" },
                CodeChallengeAlgorithm = AuthorizationGrant.CodeChallengeAlgorithmType.S256
            };

            // Open authorization request in the browser.
            System.Diagnostics.Process.Start(_authorization_grant.AuthorizationURI.ToString());
        }

        /// <summary>
        /// Authorizes client with the instance
        /// </summary>
        /// <param name="callback">Callback URI provided by authorization server</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Access token</returns>
        public AccessToken Authorize(Uri callback, CancellationToken ct = default(CancellationToken))
        {
            var task = AuthorizeAsync(callback, ct);
            try
            {
                task.Wait(ct);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Authorizes client with the instance asynchronously
        /// </summary>
        /// <param name="callback">Callback URI provided by authorization server</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Asynchronous operation with expected access token</returns>
        public async Task<AccessToken> AuthorizeAsync(Uri callback, CancellationToken ct = default(CancellationToken))
        {
            // Get API endpoints.
            var api = await GetEndpointsAsync(ct);

            // Process response and get access token.
            var token = await _authorization_grant.ProcessResponseAsync(
                HttpUtility.ParseQueryString(callback.Query),
                api.TokenEndpoint,
                null,
                ct);

            // Save the access token.
            Properties.Settings.Default.AccessTokens[api.AuthorizationEndpoint.AbsoluteUri] = token.ToBase64String();

            return token;
        }

        /// <summary>
        /// Gets (and refreshes) access token from settings
        /// </summary>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Access token or <c>null</c> if not available</returns>
        public AccessToken GetAccessToken(CancellationToken ct = default(CancellationToken))
        {
            var task = GetAccessTokenAsync(ct);
            try
            {
                task.Wait(ct);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Gets (and refreshes) access token from settings asynchronously
        /// </summary>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Asynchronous operation with expected access token</returns>
        public async Task<AccessToken> GetAccessTokenAsync(CancellationToken ct = default(CancellationToken))
        {
            // Get API endpoints.
            var api = await GetEndpointsAsync(ct);

            AccessToken token = null;
            try
            {
                // Try to restore the access token from the settings.
                var at = Properties.Settings.Default.AccessTokens[api.AuthorizationEndpoint.AbsoluteUri];
                if (at != null)
                    token = AccessToken.FromBase64String(at);
            }
            catch (Exception) { return null; }

            if (token != null && token.Expires.HasValue && token.Expires.Value <= DateTime.Now)
            {
                // The access token expired. Try refreshing it.
                try { token = await token.RefreshTokenAsync(api.TokenEndpoint, null, ct); }
                catch (Exception) { token = null; }
            }

            return token;
        }

        /// <summary>
        /// Gets instance profile list available to the user
        /// </summary>
        /// <param name="token">Access token</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Profile list</returns>
        public JSON.Collection<Models.ProfileInfo> GetProfileList(AccessToken token, CancellationToken ct = default(CancellationToken))
        {
            var task = GetProfileListAsync(token, ct);
            try
            {
                task.Wait(ct);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Gets instance profile list available to the user asynchronously
        /// </summary>
        /// <param name="token">Access token</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Asynchronous operation with expected profile list</returns>
        public async Task<JSON.Collection<Models.ProfileInfo>> GetProfileListAsync(AccessToken token, CancellationToken ct = default(CancellationToken))
        {
            if (_profile_list == null)
            {
                // Get API endpoints.
                var api = await GetEndpointsAsync(ct);

                try
                {
                    // Get and load profile list.
                    _profile_list = new JSON.Collection<Models.ProfileInfo>();
                    _profile_list.LoadJSONAPIResponse((await JSON.Response.GetAsync(
                        uri: api.ProfileList,
                        token: token,
                        ct: ct)).Value, "profile_list", ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { throw new AggregateException(Resources.Strings.ErrorProfileListLoad, ex); }
            }

            return _profile_list;
        }

        /// <summary>
        /// Gets instance user info
        /// </summary>
        /// <param name="token">Access token</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>User info</returns>
        public UserInfo GetUserInfo(AccessToken token, CancellationToken ct = default(CancellationToken))
        {
            var task = GetUserInfoAsync(token, ct);
            try
            {
                task.Wait(ct);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Gets instance user info asynchronously
        /// </summary>
        /// <param name="token">Access token</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Asynchronous operation with expected user info</returns>
        public async Task<UserInfo> GetUserInfoAsync(AccessToken token, CancellationToken ct = default(CancellationToken))
        {
            // Get API endpoints.
            var api = await GetEndpointsAsync(ct);
            if (api.UserInfo == null)
                return null;

            try
            {
                // Get and load user info.
                var user_info = new UserInfo();
                user_info.LoadJSONAPIResponse((await JSON.Response.GetAsync(
                    uri: api.UserInfo,
                    token: token,
                    ct: ct)).Value, "user_info", ct);
                return user_info;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new AggregateException(Resources.Strings.ErrorUserInfoLoad, ex); }
        }

        /// <summary>
        /// Gets client certificate
        /// </summary>
        /// <param name="token">Access token</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Client certificate. Certificate (including the private key) is saved to user certificate store.</returns>
        public X509Certificate2 GetClientCertificate(AccessToken token, CancellationToken ct = default(CancellationToken))
        {
            var task = GetClientCertificateAsync(token, ct);
            try
            {
                task.Wait(ct);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Gets client certificate asynchronously
        /// </summary>
        /// <param name="token">Access token</param>
        /// <param name="ct">The token to monitor for cancellation requests</param>
        /// <returns>Asynchronous operation with expected client certificate. Certificate (including the private key) is saved to user certificate store.</returns>
        public async Task<X509Certificate2> GetClientCertificateAsync(AccessToken token, CancellationToken ct = default(CancellationToken))
        {
            if (_client_certificate == null)
            {
                // Open eduVPN client certificate store.
                var store = new X509Store("org.eduvpn.app", StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                try
                {
                    if (Properties.Settings.Default.InstanceSettings.TryGetValue(Base.AbsoluteUri, out var instance_settings) && instance_settings.ClientCertificateHash != null)
                    {
                        // Try to restore previously issued client certificate from the certificate store first.
                        foreach (var cert in store.Certificates)
                        {
                            if (DateTime.Now < cert.NotAfter && cert.HasPrivateKey)
                            {
                                // Not expired && Has the private key.
                                if (cert.GetCertHash().SequenceEqual(instance_settings.ClientCertificateHash))
                                {
                                    // Certificate found.
                                    _client_certificate = cert;
                                }
                            }
                            else
                            {
                                // Certificate expired or matching private key not found == Useless. Clean it from the store.
                                store.Remove(cert);
                            }
                        }
                    }

                    if (_client_certificate == null)
                    {
                        // Get API endpoints.
                        var api = await GetEndpointsAsync(ct);

                        try
                        {
                            // Get certificate and import it to Windows user certificate store.
                            var cert = new Models.Certificate();
                            cert.LoadJSONAPIResponse((await JSON.Response.GetAsync(
                                uri: api.CreateCertificate,
                                param: new NameValueCollection
                                {
                                    { "display_name", Resources.Strings.CertificateTitle }
                                },
                                token: token,
                                ct: ct)).Value, "create_keypair", ct);
                            store.Add(cert.Value);
                            _client_certificate = cert.Value;

                            if (instance_settings == null)
                                Properties.Settings.Default.InstanceSettings[Base.AbsoluteUri] = instance_settings = new Models.InstanceSettings() { ClientCertificateHash = _client_certificate.GetCertHash() };
                            else
                                Properties.Settings.Default.InstanceSettings[Base.AbsoluteUri].ClientCertificateHash = _client_certificate.GetCertHash();
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { throw new AggregateException(Resources.Strings.ErrorClientCertificateLoad, ex); }
                    }
                }
                finally { store.Close(); }
            }

            return _client_certificate;
        }

        #endregion

        #region ILoadableItem Support

        /// <summary>
        /// Loads instance from a dictionary object (provided by JSON)
        /// </summary>
        /// <param name="obj">Key/value dictionary with <c>base_uri</c>, <c>logo</c> and <c>display_name</c> elements. <c>base_uri</c> is required. All elements should be strings.</param>
        /// <exception cref="eduJSON.InvalidParameterTypeException"><paramref name="obj"/> type is not <c>Dictionary&lt;string, object&gt;</c></exception>
        public void Load(object obj)
        {
            if (obj is Dictionary<string, object> obj2)
            {
                // Set base URI.
                Base = new Uri(eduJSON.Parser.GetValue<string>(obj2, "base_uri"));

                // Set display name.
                DisplayName = eduJSON.Parser.GetLocalizedValue(obj2, "display_name", out string display_name) ? display_name : Base.Host;

                // Set logo URI.
                Logo = eduJSON.Parser.GetLocalizedValue(obj2, "logo", out string logo_uri) ? new Uri(logo_uri) : null;
            }
            else
                throw new eduJSON.InvalidParameterTypeException("obj", typeof(Dictionary<string, object>), obj.GetType());
        }

        #endregion

        #region IXmlSerializable Support

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            string v;

            Base = (v = reader.GetAttribute("Base")) != null ? new Uri(v) : null;
            DisplayName = reader.GetAttribute("DisplayName");
            Logo = (v = reader.GetAttribute("Logo")) != null ? new Uri(v) : null;
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("Base", Base.AbsoluteUri);
            if (DisplayName != null)
                writer.WriteAttributeString("DisplayName", DisplayName);
            if (Logo != null)
                writer.WriteAttributeString("Logo", Logo.AbsoluteUri);
        }

        #endregion
    }
}
