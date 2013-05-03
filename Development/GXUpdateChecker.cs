//
// --------------------------------------------------------------------------
//  Gurux Ltd
// 
//
//
// Filename:        $HeadURL$
//
// Version:         $Revision$,
//                  $Date$
//                  $Author$
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License 
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
// See the GNU General Public License for more details.
//
// This code is licensed under the GNU General Public License v2. 
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.Zip;
using System.Threading;
using Microsoft.Win32;

namespace Gurux.Common
{
    /// <summary>
    /// Is there any updates.
    /// </summary>
    public enum ProtocolUpdateStatus : int
    {
        /// <summary>
        /// There are no new updates available.
        /// </summary>
        None = 0,
        /// <summary>
        /// Updates are installed.
        /// </summary>
        Changed = 1,
        /// <summary>
        /// Updates are installed and restart is required.
        /// </summary>
        Restart = 2
    }

    /// <summary>
    /// Represents the method that will handle the event that has no event data.
    /// </summary>
    public delegate void CheckUpdatesEventHandler();

    /// <summary>
    /// This class is used to check new updates from Gurux web pages.
    /// </summary>
    public class GXUpdateChecker
    {
        static readonly object m_sync = new object();
        internal delegate void ProgressEventHandler(GXAddIn sender);
		private event CheckUpdatesEventHandler m_OnCheckUpdates;

        
        [System.Runtime.InteropServices.DllImport("wininet.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        internal extern static bool InternetGetConnectedState(ref InternetConnectionState lpdwFlags, int dwReserved);        

        [Flags]
        internal enum InternetConnectionState : int
        {
            Modem = 0x1,
            Lan = 0x2,
            Proxy = 0x4,
            RasInstalled = 0x10,
            Offline = 0x20,
            Configured = 0x40
        }

        GXAddIn Target;
        AutoResetEvent Downloaded = new AutoResetEvent(false);       

        /// <summary>
        /// Update protocols from the Gurux www server.
        /// </summary>        
        public ProtocolUpdateStatus UpdateProtocols()
        {
            lock (m_sync)
            {
                string backupPath = Path.Combine(GXCommon.ProtocolAddInsPath, "backup");
                if (!System.IO.Directory.Exists(backupPath))
                {
                    System.IO.Directory.CreateDirectory(backupPath);
                    Gurux.Common.GXFileSystemSecurity.UpdateDirectorySecurity(backupPath);
                }
                DataContractSerializer x = new DataContractSerializer(typeof(GXAddInList));
                GXAddInList localAddins;
                string path = Path.Combine(GXCommon.ProtocolAddInsPath, "updates.xml");
                ProtocolUpdateStatus status = ProtocolUpdateStatus.None;
                if (!System.IO.File.Exists(path))
                {
                    return status;
                }
                using (FileStream reader = new FileStream(path, FileMode.Open))
                {
                    localAddins = (GXAddInList)x.ReadObject(reader);
                }
                System.Net.WebClient client = new System.Net.WebClient();                
				client.DownloadProgressChanged += new System.Net.DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
				client.DownloadDataCompleted += new System.Net.DownloadDataCompletedEventHandler(client_DownloadDataCompleted);				
                foreach (GXAddIn it in localAddins)
                {
                    if (it.Type != GXAddInType.AddIn)
                    {
                        continue;
                    }
                    if (it.State == AddInStates.Download || it.State == AddInStates.Update)
                    {						
                        string AddInPath = Path.Combine(GXCommon.ProtocolAddInsPath, it.File);
                        if (it.Type == GXAddInType.AddIn ||
                            it.Type == GXAddInType.None)
                        {
                            AddInPath = GXCommon.ProtocolAddInsPath;
                        }
                        else if (it.Type == GXAddInType.Application)
                        {
                            AddInPath = Path.GetDirectoryName(typeof(GXUpdateChecker).Assembly.Location);
                        }
                        else 
                        {
                            throw new Exception("Unknown type " + it.Type.ToString());
                        }
                        try
                        {							
                            string ext = Path.GetExtension(it.File);
                            if (string.Compare(ext, ".zip", true) == 0 ||
                                string.Compare(ext, ".msi", true) == 0)
                            {
                                Target = it;
                                string tmpPath = Path.Combine(System.IO.Path.GetTempPath(), it.File);                                
								Downloaded.Reset();
                                if (string.Compare(ext, ".zip", true) == 0)
                                {
                                    client.DownloadDataAsync(new Uri("http://www.gurux.org/updates/" + it.File), tmpPath);
                                }
                                else //If .msi
                                {
                                    client.DownloadDataAsync(new Uri("http://www.gurux.org/Downloads/" + it.File), tmpPath);
                                }
                                
                                while (!Downloaded.WaitOne(100))
                                {
                                    Application.DoEvents();                                    
                                }
                                if (string.Compare(ext, ".zip", true) == 0)
                                {
                                    ZipInputStream s = new ZipInputStream(File.OpenRead(tmpPath));
                                    ZipEntry theEntry;
                                    byte[] data = new byte[2000]; //2MB buffer
                                    while ((theEntry = s.GetNextEntry()) != null)
                                    {
                                        if (theEntry.IsFile)
                                        {
                                            string FileName = Path.Combine(AddInPath, Path.GetFileName(theEntry.Name));
                                            int size;
                                            if (File.Exists(FileName))
                                            {
                                                status |= ProtocolUpdateStatus.Restart;
                                                FileName = Path.Combine(Path.GetDirectoryName(FileName), "cached");
                                                if (!Directory.Exists(FileName))
                                                {
                                                    Directory.CreateDirectory(FileName);
                                                    Gurux.Common.GXFileSystemSecurity.UpdateDirectorySecurity(FileName);
                                                }
                                                FileName = Path.Combine(FileName, Path.GetFileName(theEntry.Name));
                                            }
                                            if (File.Exists(FileName))
                                            {
                                                File.Delete(FileName);
                                            }
                                            using (BinaryWriter b = new BinaryWriter(File.Create(FileName)))
                                            {
                                                do
                                                {
                                                    size = s.Read(data, 0, 2000);
                                                    if (size > 0)
                                                    {
                                                        b.Write(data, 0, size);
                                                    }
                                                    else
                                                    {
                                                        b.Close();
                                                    }
                                                }
                                                while (size > 0);
                                                ext = Path.GetExtension(FileName);
                                                if (string.Compare(ext, ".dll", true) == 0 || string.Compare(ext, ".exe", true) == 0)
                                                {
                                                    System.Reflection.Assembly asm = System.Reflection.Assembly.LoadFile(FileName);
                                                    System.Diagnostics.FileVersionInfo newVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location);
                                                    it.Version = it.InstalledVersion = newVersion.FileVersion;
                                                }
                                            }
                                            Gurux.Common.GXFileSystemSecurity.UpdateFileSecurity(FileName);
                                        }
                                    }
                                }
                                else //If .msi
                                {
                                    Process msi = new Process();
                                    msi.StartInfo.FileName = "msiexec.exe";
                                    msi.StartInfo.Arguments = "/i \"" + tmpPath + "\" /qr";
                                    msi.Start();
                                }
                            }
                            else
                            {
                                AddInPath = Path.Combine(AddInPath, it.File);
                                System.IO.File.WriteAllBytes(AddInPath, client.DownloadData("http://www.gurux.org/updates/" + it.File));
                                Gurux.Common.GXFileSystemSecurity.UpdateFileSecurity(AddInPath);
                                System.Reflection.Assembly asm = System.Reflection.Assembly.LoadFile(AddInPath);
                                System.Diagnostics.FileVersionInfo newVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location);
                                it.Version = it.InstalledVersion = newVersion.FileVersion;
                            }
                        }
                        //If file is in use.
                        catch (System.IO.IOException)
                        {
                            string cachedPath = Path.Combine(GXCommon.ProtocolAddInsPath, "cached");
                            if (!Directory.Exists(cachedPath))
                            {
                                Directory.CreateDirectory(cachedPath);
                                Gurux.Common.GXFileSystemSecurity.UpdateDirectorySecurity(cachedPath);
                            }
                            cachedPath = Path.Combine(cachedPath, it.File);
                            System.IO.File.WriteAllBytes(cachedPath, client.DownloadData("http://www.gurux.org/updates/" + it.File));
                            Gurux.Common.GXFileSystemSecurity.UpdateFileSecurity(cachedPath);
                            AppDomain domain = AppDomain.CreateDomain("import", null, AppDomain.CurrentDomain.SetupInformation);
                            //Get version number and unload assmbly.                          
                            System.Reflection.Assembly asm = domain.Load(System.Reflection.AssemblyName.GetAssemblyName(cachedPath));
                            System.Diagnostics.FileVersionInfo newVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location);
                            it.Version = it.InstalledVersion = newVersion.FileVersion;
                            AppDomain.Unload(domain); 
                            status |= ProtocolUpdateStatus.Restart;
                        }
                        status |= ProtocolUpdateStatus.Changed;
                        it.State = AddInStates.Installed;
                    }
                }
                if ((status & ProtocolUpdateStatus.Changed) != 0)
                {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.Encoding = System.Text.Encoding.UTF8;
                    settings.CloseOutput = true;
                    settings.CheckCharacters = false;
                    using (XmlWriter writer = XmlWriter.Create(path, settings))
                    {
                        x.WriteObject(writer, localAddins);
                        writer.Close();
                    }
                    Gurux.Common.GXFileSystemSecurity.UpdateFileSecurity(path);
                    //TODO: GXDeviceList.UpdateProtocols();
                }
                return status;
            }
        }

        void client_DownloadDataCompleted(object sender, System.Net.DownloadDataCompletedEventArgs e)
        {
            if (!e.Cancelled && e.Error == null)
            {
                System.IO.File.WriteAllBytes(e.UserState.ToString(), e.Result);
                Downloaded.Set();
            }
            if (OnProgress != null)
            {
                Target.ProgressPercentage = 100;
                OnProgress(Target);
            }
        }

        void client_DownloadProgressChanged(object sender, System.Net.DownloadProgressChangedEventArgs e)
        {
            if (OnProgress != null)
            {
                Target.ProgressPercentage = e.ProgressPercentage;
                OnProgress(Target);
            }
        }

        /// <summary>
        /// Show updates.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="showAddins"></param>
        /// <param name="onlyNew"></param>
        /// <returns></returns>
        public static ProtocolUpdateStatus ShowUpdates(System.Windows.Forms.IWin32Window owner, bool showAddins, bool onlyNew)
        {
            string[] DisabledItems;
            return ShowUpdates(owner, showAddins, onlyNew, out DisabledItems);
        }

        /// <summary>
        /// Show updates.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="showAddins"></param>
        /// <param name="onlyNew"></param>
        /// <param name="DisabledItems">collection of disabled addins.</param>
        /// <returns></returns>
        public static ProtocolUpdateStatus ShowUpdates(System.Windows.Forms.IWin32Window owner, bool showAddins, bool onlyNew, out string[] DisabledItems)
        {
            GXAddInList localAddins;
            string path = Path.Combine(GXCommon.ProtocolAddInsPath, "updates.xml");
            DataContractSerializer x = new DataContractSerializer(typeof(GXAddInList));
            lock (m_sync)
            {
                if (!System.IO.File.Exists(path))
                {
                    localAddins = new GXAddInList();
                }
                else
                {
                    try
                    {
                        using (FileStream reader = new FileStream(path, FileMode.Open))
                        {
                            localAddins = (GXAddInList)x.ReadObject(reader);
                        }
                    }
                    catch (Exception)
                    {
                        try
                        {
                            File.Delete(path);
                        }
                        catch
                        {
                            //It's OK if this fails.
                        }
                        localAddins = new GXAddInList();
                    }
                }
            }
            AddInsForm dlg = new AddInsForm(localAddins, showAddins, onlyNew);
            if (dlg.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
            {
                List<string> items = new List<string>();
                foreach (GXAddIn it in localAddins)
                {
                    if (it.Type == GXAddInType.AddIn && (it.State & AddInStates.Disabled) != 0)
                    {
                        items.Add(it.Name);
                    }
                }
                DisabledItems = items.ToArray();
                return dlg.Status;
            }
            DisabledItems = new string[0];
            return ProtocolUpdateStatus.None;
        }

        /// <summary>
        /// Only Application updates are shown. Protocol AddIn or Media updates are not shown.
        /// </summary>
        public static bool ApplicationsOnly = false;
       
        /// <summary>
        /// Is there any new updates avaialble.
        /// </summary>
        /// <returns>Returns True, if new updates are available.</returns>
        public static bool IsUpdatesOnline()
        {
            return GetUpdatesOnline().Count != 0;
        }

        /// <summary>
        /// Compares version strings. A ersion string is assumed to 
        /// contain four digits separated by '.' or ',', for example "1.2.3.4".
        /// </summary>
        /// <param name="newVersion"></param>
        /// <param name="installerVersion"></param>
        /// <returns></returns>
        static public bool IsNewVersion(string newVersion, string installerVersion)
        {
            if (string.IsNullOrEmpty(newVersion) ||
                string.IsNullOrEmpty(installerVersion))
            {
                return false;
            }
            string[] newVersions = newVersion.Split(new char[] { '.', ',' });
            string[] oldVersions = installerVersion.Split(new char[] { '.', ',' });
            if (newVersions.Length != oldVersions.Length)
            {
                return false;
            }
            int cnt = Math.Min(newVersions.Length, oldVersions.Length);
            for (int pos = 0; pos != cnt; ++pos)
            {
                int newV = int.Parse(newVersions[pos]);
                int oldV = int.Parse(oldVersions[pos]);
                if (newV != oldV)
                {
                    return newV > oldV;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if there are any updates available in Gurux www server.
        /// </summary>
        /// <returns>Returns true if there are any updates available.</returns>
        internal static GXAddInList GetUpdatesOnline()
        {
			lock (m_sync)
			{
				try
				{
                    //Do not check updates while debugging.
                    string path = Path.Combine(GXCommon.ProtocolAddInsPath, "updates.xml");
					DataContractSerializer x = new DataContractSerializer(typeof(GXAddInList));
					System.Net.WebClient client = new System.Net.WebClient();
					GXAddInList onlineAddIns, localAddins;
					// Put the byte array into a stream and rewind it to the beginning 
                    using (MemoryStream ms = new MemoryStream(client.DownloadData("http://www.gurux.org/updates/updates.xml")))
					{
						ms.Flush();
						ms.Position = 0;
						onlineAddIns = (GXAddInList)x.ReadObject(ms);
					}
					GXAddInList newItems = new GXAddInList();
					if (System.IO.File.Exists(path))
					{
						using (FileStream reader = new FileStream(path, FileMode.Open))
						{
							try
							{
								localAddins = (GXAddInList)x.ReadObject(reader);
							}
							catch
							{
								localAddins = new GXAddInList();
							}
						}
						foreach (GXAddIn it in onlineAddIns)
						{
                            //Check only applications updates.
                            if (ApplicationsOnly && it.Type != GXAddInType.Application)
                            {
                                continue;
                            }
							GXAddIn localAddin = localAddins.FindByName(it.Name);
							if (localAddin == null)
							{
                                if (string.Compare(Path.GetFileNameWithoutExtension(Application.ExecutablePath), it.Name, true) != 0)
                                {
                                    newItems.Add(it);
                                }
                                else
                                {
                                    //Get version info
                                    System.Reflection.Assembly ass = System.Reflection.Assembly.GetEntryAssembly();
                                    System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(ass.Location);
                                    it.InstalledVersion = info.FileVersion;
                                    if (it.Version == info.FileVersion)
                                    {
                                        it.State = AddInStates.Installed;
                                    }
                                    else
                                    {
                                        newItems.Add(it);
                                    }
                                }
								localAddins.Add(it);
							}
                            else //Compare versions.
							{
                                bool newVersion = IsNewVersion(it.Version, localAddin.InstalledVersion);
                                if ((localAddin.State & AddInStates.Disabled) == 0 && (localAddin.State == AddInStates.Available || newVersion))
                                {
                                    if (it.Type == GXAddInType.Application && string.Compare(Path.GetFileNameWithoutExtension(Application.ExecutablePath), it.Name, true) != 0)
                                    {
                                        continue;
                                    }
                                    localAddin.Version = it.Version;
                                    localAddin.State = AddInStates.Available;
                                    if (newVersion)
                                    {
                                        localAddin.State = AddInStates.Update;
                                    }
                                    localAddin.Version = it.Version;
                                    newItems.Add(localAddin);
                                }
							}
                            if (localAddin != null && string.IsNullOrEmpty(it.InstalledVersion) && it.Type == GXAddInType.Application &&
                                    string.Compare(Path.GetFileNameWithoutExtension(Application.ExecutablePath), it.Name, true) == 0)
                            {
                                //Get version info
                                System.Reflection.Assembly ass = System.Reflection.Assembly.GetEntryAssembly();
                                System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(ass.Location);
                                localAddin.InstalledVersion = info.FileVersion;
                                if (localAddin.Version == info.FileVersion)
                                {
                                    localAddin.State = AddInStates.Installed;
                                }
                                bool newVersion = IsNewVersion(it.Version, localAddin.InstalledVersion);
                                if (newVersion)
                                {
                                    localAddin.State = AddInStates.Update;
                                }
                            }
						}
					}
					else
					{
						newItems = localAddins = onlineAddIns;
                        //Update product version.
                        foreach (GXAddIn it in onlineAddIns)
                        {
                            if (string.Compare(Path.GetFileNameWithoutExtension(Application.ExecutablePath), it.Name, true) == 0)
                            {
                                //Get version info
                                System.Reflection.Assembly ass = System.Reflection.Assembly.GetEntryAssembly();
                                System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(ass.Location);
                                it.InstalledVersion = info.FileVersion;
                                if (it.Version == info.FileVersion)
                                {
                                    it.State = AddInStates.Installed;
                                }
                                break;
                            }
                        }
					}
					if (newItems.Count != 0)
					{
						XmlWriterSettings settings = new XmlWriterSettings();
						settings.Indent = true;
						settings.Encoding = System.Text.Encoding.UTF8;
						settings.CloseOutput = true;
						settings.CheckCharacters = false;
                        string tmp = Path.GetDirectoryName(path);
                        if (!System.IO.Directory.Exists(tmp))
                        {
                            Directory.CreateDirectory(tmp);
                            Gurux.Common.GXFileSystemSecurity.UpdateDirectorySecurity(tmp);
                        }
						using (XmlWriter writer = XmlWriter.Create(path, settings))
						{
							x.WriteObject(writer, localAddins);
							writer.Close();
						}
                        Gurux.Common.GXFileSystemSecurity.UpdateFileSecurity(path);
					}
					return newItems;
				}
				catch(Exception ex)
				{
                    System.Diagnostics.Debug.WriteLine(ex.Message);
					return new GXAddInList();
				}
			}
        }

        /// <summary>
        /// Check if new updates are available.
        /// </summary>
        /// <param name="target"></param>
        static public void CheckUpdates(object target)
        {
            try
            {
                //Do not check updates while debugging.
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    return;
                }
                DateTime LastUpdateCheck = DateTime.MinValue;
                //Wait for a while before check updates.
                //Check new updates once a day.
                while (true)
                {
                    if (LastUpdateCheck.AddDays(1) < DateTime.Now)
                    {
                        LastUpdateCheck = DateTime.Now;
						bool isConnected = true;
						if (System.Environment.OSVersion.Platform != PlatformID.Unix)
						{
                        	InternetConnectionState flags = InternetConnectionState.Lan | InternetConnectionState.Configured;
                        	isConnected = InternetGetConnectedState(ref flags, 0);
						}
                        //If there are updates available.
                        if (isConnected && IsUpdatesOnline())
                        {
                            if (target != null)
                            {
                                ((CheckUpdatesEventHandler)target)();
                            }
                            break;
                        }
                    }
                    //Wait for a day before next check.
                    System.Threading.Thread.Sleep(DateTime.Now.AddDays(1) - DateTime.Now);
                }
            }
            catch
            {
                //It's OK if this fails.
            }
        }

		/// <summary>
		/// Represents the method that will handle the event that has no event data.
		/// </summary>
		public event CheckUpdatesEventHandler OnCheckUpdates
		{
			add
			{
				m_OnCheckUpdates += value;
			}
			remove
			{
				m_OnCheckUpdates += value;
			}
		}

        internal event ProgressEventHandler OnProgress;
    }
}