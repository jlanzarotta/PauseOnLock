#region Source File Header
//******************************************************************************
//    Copyright: Copyright (c) 2014 by Jeff Lanzarotta.
//               All rights reserved.
//
//               Redistribution and use in source and binary forms, with or
//               without modification, are permitted provided that the
//               following conditions are met:
//
//               * Redistributions of source code must retain the above
//                 copyright notice, this list of conditions and the following
//                 disclaimer.
//
//               * Redistributions in binary form must reproduce the above
//                 copyright notice, this list of conditions and the following
//                 disclaimer in the documentation and/or other materials
//                 provided with the distribution.
//
//               * Neither the name of the {organization} nor the names of its
//                 contributors may be used to endorse or promote products
//                 derived from this software without specific prior written
//                 permission.
//
//               THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
//               CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
//               INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
//               MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//               DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
//               CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
//               SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
//               NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
//               LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
//               HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
//               CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
//               OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
//               EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// Name Of File: PauseOnLock.cs
//  Description: Pause On Lock plugin for MusicBee.
//   Maintainer: Jeff Lanzarotta
// Last Changed: Monday, 07 July 2014
//      Version: 1.0.1
//******************************************************************************
#endregion

using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Text;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        // Public Methods and Properties ---------------------------------------

        /// <summary>
        /// Initialises the specified API interface PTR.
        /// </summary>
        /// <param name="apiInterfacePtr">The API interface PTR.</param>
        /// <returns></returns>
        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            // "©".
            byte[] newBytes = new Byte[] { 169 };
            var encoding = Encoding.GetEncoding(1252);
            String copyright = encoding.GetString(newBytes, 0, newBytes.Length);

            // Get current date/time.
            DateTime now = System.DateTime.Now;

            _mbApiInterface = new MusicBeeApiInterface();
            _mbApiInterface.Initialise(apiInterfacePtr);
            _about.PluginInfoVersion = PluginInfoVersion;
            _about.Name = "Pause On Lock";
            _about.Description = "Pause/Resume when Workstation is Locked/Unlocked.";
            _about.Author = copyright + " Copyright " + now.Year + " Jeff Lanzarotta";
            _about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = PluginType.General;
            _about.VersionMajor = 1;  // your plugin version
            _about.VersionMinor = 1;
            _about.Revision = 1;
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            _about.ConfigurationPanelHeight = 0;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            _workLock = new CheckForWorkstationLocking(_mbApiInterface);
            _workLock.Run();

            return (_about);
        }

        /// <summary>
        /// Configures the specified panel handle.
        /// </summary>
        /// <param name="panelHandle">The panel handle.</param>
        /// <returns></returns>
        public bool Configure(IntPtr panelHandle)
        {
            // Save any persistent settings in a sub-folder of this path.
            String dataPath =
                _mbApiInterface.Setting_GetPersistentStoragePath();

            // PanelHandle will only be set if you set
            // about.ConfigurationPanelHeight to a non-zero value keep in mind
            // the panel width is scaled according to the font the user has
            // selected if about.ConfigurationPanelHeight is set to 0, you can
            // display your own popup window.
            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(0, 0);
                prompt.Text = "prompt:";
                TextBox textBox = new TextBox();
                textBox.Bounds = new Rectangle(60, 0, 100, textBox.Height);
                configPanel.Controls.AddRange(new Control[] { prompt, textBox });
            }

            return (false);
        }

        /// <summary>
        /// MusicBee is closing the plugin (plugin is being disabled by user or
        /// MusicBee is shutting down).
        /// </summary>
        /// <param name="reason">The reason.</param>
        public void Close(PluginCloseReason reason)
        {
            if (_workLock != null)
            {
                _workLock.Dispose();
                _workLock = null;
            }
        }

        /// <summary>
        /// Receives event notifications from MusicBee.
        /// You need to set about.ReceiveNotificationFlags = PlayerEvents to
        /// receive all notifications, and not just the startup event.
        /// </summary>
        /// <param name="sourceFileUrl">The source file URL.</param>
        /// <param name="type">The type.</param>
        public void ReceiveNotification(String sourceFileUrl,
            NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialization
                    switch (_mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                        case PlayState.Paused:
                            // ...
                            break;
                    }
                    break;
            }
        }

        /// <summary>
        /// Called by MusicBee when the user clicks Apply or Save in the
        /// MusicBee Preferences screen.  Its up to you to figure out whether
        /// anything has changed and needs updating.
        /// </summary>
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
            String dataPath =
                _mbApiInterface.Setting_GetPersistentStoragePath();
        }

        /// <summary>
        /// Uninstall this plugin.  Clean up any persisted files.
        /// </summary>
        public void Uninstall()
        {
        }

        // Private Data --------------------------------------------------------

        private MusicBeeApiInterface _mbApiInterface;
        private PluginInfo _about = new PluginInfo();
        private CheckForWorkstationLocking _workLock = null;
    }

    /// <summary>
    /// Monitors to see if the Workstation is being locked/unlocked.  In either
    /// situation, the delegate Player_PlayPause is called.
    /// </summary>
    public class CheckForWorkstationLocking : IDisposable
    {
        // Constructor/Destructor ----------------------------------------------

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckForWorkstationLocking"/> class.
        /// </summary>
        /// <param name="mbApiInterface">The mb API interface.</param>
        public CheckForWorkstationLocking(Plugin.MusicBeeApiInterface
            mbApiInterface)
        {
            _mbApiInterface = mbApiInterface;
        }

        // Public Methods and Properties ---------------------------------------

        /// <summary>
        /// Runs this instance.
        /// </summary>
        public void Run()
        {
            _sseh = new SessionSwitchEventHandler(SysEventsCheck);
            SystemEvents.SessionSwitch += _sseh;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_sseh != null)
            {
                SystemEvents.SessionSwitch -= _sseh;
            }
        }

        // Private Methods and Properties --------------------------------------

        /// <summary>
        /// Method called when a SystemEvent occurs.  When the session is
        /// locked, the Player is paused and when the session is unlocked, the
        /// Player is set to Play.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SessionSwitchEventArgs"/> instance containing the event data.</param>
        private void SysEventsCheck(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    // If the player is playing, then pause it.
                    if (_mbApiInterface.Player_GetPlayState() ==
                        Plugin.PlayState.Playing)
                    {
                        _wasPlayerPlaying = true;
                        _mbApiInterface.Player_PlayPause();
                    }
                    else
                    {
                        _wasPlayerPlaying = false;
                    }
                    break;
                case SessionSwitchReason.SessionUnlock:
                    if (_wasPlayerPlaying == true)
                    {
                        _mbApiInterface.Player_PlayPause();
                    }
                    break;
            }
        }

        // Private Data --------------------------------------------------------

        private bool _wasPlayerPlaying = false;
        private SessionSwitchEventHandler _sseh = null;
        private Plugin.MusicBeeApiInterface _mbApiInterface;
    }
}
