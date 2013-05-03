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
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;
using System.Xml;
using System.IO;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Text;
using System.Linq;

namespace Gurux.Common
{
	/// <summary>
	/// Common Gurux helpers. 
	/// </summary>
	public class GXCommon
	{
        /// <summary>
        /// Check that correct framework is installed.
        /// </summary>
        static public void CheckFramework()
        {
            //Is .Net 4.0 client installed.
            const string net40 = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client";
            using (RegistryKey subKey = Registry.LocalMachine.OpenSubKey(net40))
            {
                if (subKey != null && Convert.ToUInt32(subKey.GetValue("Install")) == 1)
                {
                    //Everything is OK.
                    return;
                }
            }

            //Is .Net 3.5 installed.
            const string net35 = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5";
            using (RegistryKey subKey = Registry.LocalMachine.OpenSubKey(net35))
            {
                if (subKey != null && Convert.ToUInt32(subKey.GetValue("Install")) == 1)
                {
                    string version = Convert.ToString(subKey.GetValue("Version"));
                    string servicePack = Convert.ToString(subKey.GetValue("SP"));
                    string str = ".NET Framework 3.5";
                    if (string.IsNullOrEmpty(servicePack))
                    {
                        throw new Exception(".Net framework 3.5 SP1 must be installed before the application can be used.");
                    }
                    return;
                }
            }
            throw new Exception(".Net framework 3.5 SP1 or 4.0 must be installed before the application can be used.");
        }

		/// <summary>
		/// Title of messagebox
		/// </summary>
		public static string Title = "";
		/// <summary>
		/// Parent window of messagebox
		/// </summary>
		public static IWin32Window Owner = null;

        /// <summary>
        /// Convert bytearray to hex string.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="addSpace"></param>
        /// <returns></returns>
        public static string ToHex(byte[] bytes, bool addSpace)
        {
            char[] c = new char[bytes.Length * (addSpace ? 3 : 2)];
            byte b;
            for (int bx = 0, cx = 0; bx != bytes.Length; ++bx, ++cx)
            {
                b = ((byte)(bytes[bx] >> 4));
                c[cx] = (char)(b > 9 ? b + 0x37 + 0x20 : b + 0x30);
                b = ((byte)(bytes[bx] & 0x0F));
                c[++cx] = (char)(b > 9 ? b + 0x37 + 0x20 : b + 0x30);
                if (addSpace)
                {
                    c[++cx] = ' ';
                }
            }

            return new string(c);
        }

        /// <summary>
        /// Convert string to byte array.
        /// </summary>
        /// <param name="str">Hex string</param>
        /// <param name="includeSpace">Is there space between hex values.</param>
        /// <returns>Byte array.</returns>
        public static byte[] HexToBytes(string str, bool includeSpace)
        {
            int cnt = includeSpace ? 3 : 2;
            if (str.Length == 0 || str.Length % cnt != 0)
            {
                throw new ArgumentException("Not hex string");
            }
            byte[] buffer = new byte[str.Length / cnt];
            char c;
            for (int bx = 0, sx = 0; bx < buffer.Length; ++bx, ++sx)
            {
                c = str[sx];
                buffer[bx] = (byte)((c > '9' ? (c > 'Z' ? (c - 'a' + 10) : (c - 'A' + 10)) : (c - '0')) << 4);
                c = str[++sx];
                buffer[bx] |= (byte)(c > '9' ? (c > 'Z' ? (c - 'a' + 10) : (c - 'A' + 10)) : (c - '0'));
                if (includeSpace)
                {
                    ++sx;
                }
            }
            return buffer;
        }

		/// <summary>
		/// Writes a timestamped line using System.Diagnostics.Trace.WriteLine
		/// </summary>
		public static void TraceWriteLine(string line)
		{
			System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff") + " " + line);
		}

		/// <summary>
		/// Writes a timestamped string using System.Diagnostics.Trace.Write
		/// </summary>
		public static void TraceWrite(string text)
		{
			System.Diagnostics.Trace.Write(DateTime.Now.ToString("HH:mm:ss.ffff") + " " + text);
		}

		/// <summary>
		/// Convert object to byte array.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static byte[] GetAsByteArray(object value)
		{
			if (value == null)
			{
				return new byte[0];
			}
			if (value is string)
			{
				return Encoding.UTF8.GetBytes((string)value);
			}
			int rawsize = 0;
			byte[] rawdata = null;
			GCHandle handle;
			if (value is Array)
			{
				Array arr = value as Array;
				if (arr.Length != 0)
				{
					int valueSize = Marshal.SizeOf(arr.GetType().GetElementType());
					rawsize = valueSize * arr.Length;
					rawdata = new byte[rawsize];
					handle = GCHandle.Alloc(rawdata, GCHandleType.Pinned);
					long pos = handle.AddrOfPinnedObject().ToInt64();
					foreach (object it in arr)
					{
						Marshal.StructureToPtr(it, new IntPtr(pos), false);
						pos += valueSize;
					}
					handle.Free();
					return rawdata;
				}
				return new byte[0];
			}

			rawsize = Marshal.SizeOf(value);
			rawdata = new byte[rawsize];
			handle = GCHandle.Alloc(rawdata, GCHandleType.Pinned);
			Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
			handle.Free();
			return rawdata;
		}

        /// <summary>
        /// Convert byte array to object.
        /// </summary>
        /// <param name="byteArray"></param>
        /// <param name="type"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <param name="reverse"></param>
        /// <param name="readBytes"></param>
        /// <returns></returns>
		public static object ByteArrayToObject(byte[] byteArray, Type type, int index, int count, bool reverse, out int readBytes)
		{
			if (byteArray == null)
			{
				throw new ArgumentException("byteArray");
			}
			if (count <= 0)
			{
				count = byteArray.Length - index;
			}
			//If count is higger than one and type is not array.
			if (count != 1 && !type.IsArray && type != typeof(string))
			{
				throw new ArgumentException("count");
			}
			if (index < 0 || index > byteArray.Length)
			{
				throw new ArgumentException("index");
			}
			if (type == typeof(byte[]) && index == 0 && count == byteArray.Length)
			{
				readBytes = byteArray.Length;
				return byteArray;
			}
			readBytes = 0;
			Type valueType = null;
			int valueSize = 0;
			if (index != 0 || reverse)
			{
				if (type == typeof(string))
				{
					readBytes = count;
				}
				else if (type.IsArray)
				{
					valueType = type.GetElementType();
					valueSize = Marshal.SizeOf(valueType);
					readBytes = (valueSize * count);
				}
				else if (type == typeof(bool) || type == typeof(Boolean))
				{
					readBytes = 1;
				}
				else
				{
					readBytes = Marshal.SizeOf(type);
				}
				byte[] tmp = byteArray;
				byteArray = new byte[readBytes];
				Array.Copy(tmp, index, byteArray, 0, readBytes);
			}
			object value = null;
			if (type == typeof(string))
			{
				return Encoding.UTF8.GetString(byteArray);
			}
			else if (reverse)
			{
				byteArray = byteArray.Reverse().ToArray();
			}
			GCHandle handle;
			if (type.IsArray)
			{
				if (count == -1)
				{
					count = byteArray.Length / Marshal.SizeOf(valueType);
				}
				Array arr = (Array)Activator.CreateInstance(type, count);
				handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
				long start = handle.AddrOfPinnedObject().ToInt64();
				for (int pos = 0; pos != count; ++pos)
				{
					arr.SetValue(Marshal.PtrToStructure(new IntPtr(start), valueType), pos);
					start += valueSize;
					readBytes += valueSize;
				}
				handle.Free();
				return arr;
			}
			handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
			value = Marshal.PtrToStructure(handle.AddrOfPinnedObject(), type);
			readBytes = Marshal.SizeOf(type);
			handle.Free();
			return value;
		}

		/// <summary>
		/// Convert received byte stream to wanted object.
		/// </summary>
		/// <param name="byteArray">Bytes to parse.</param>
		/// <param name="type">Object type.</param>
		/// <param name="readBytes">Read byte count.</param>
		/// <returns></returns>
		public static object ByteArrayToObject(byte[] byteArray, Type type, out int readBytes)
		{
			return ByteArrayToObject(byteArray, type, 0, byteArray.Length, false, out readBytes);
		}

		/// <summary>
		/// Searches for the specified pattern and returns the index of the first occurrence
		/// within the range of elements in the byte buffer that starts at the specified
		/// index and contains the specified number of elements.
		/// </summary>
		/// <param name="input">Input byte buffer</param>
		/// <param name="pattern"></param>
		/// <param name="index">Index where search is started.</param>
		/// <param name="count">Maximum search buffer size.</param>
		/// <returns></returns>
		public static int IndexOf(byte[] input, byte[] pattern, int index, int count)
		{
			//If not enought data available.
			if (count < pattern.Length)
			{
				return -1;
			}
			byte firstByte = pattern[0];
			int pos = -1;
			if ((pos = Array.IndexOf(input, firstByte, index, count - index)) >= 0)
			{
				for (int i = 0; i < pattern.Length; i++)
				{
					if (pos + i >= input.Length || pattern[i] != input[pos + i])
					{
						return -1;
					}
				}
			}
			return pos;
		}

		/// <summary>
		/// Compares two byte or byte array values.
		/// </summary>
		public static bool EqualBytes(object a, object b)
		{
			if (a == null)
			{
				return b == null;
			}
			if (b == null)
			{
				return a == null;
			}
			if (a is Array && b is Array)
			{
				int pos = 0;
				if (((Array)a).Length != ((Array)b).Length)
				{
					return false;
				}
				foreach (byte mIt in (byte[])a)
				{
					if ((((byte)((byte[])b).GetValue(pos++)) & mIt) != mIt)
					{
						return false;
					}
				}
			}
			else
			{
				return BitConverter.Equals(a, b);
			}
			return true;
		}

		/// <summary>
        /// Retrieves the path to application data.
		/// </summary>
		public static string ApplicationDataPath
		{
			get
			{
				string path = string.Empty;
				if (Environment.OSVersion.Platform == PlatformID.Unix)
				{					
					path = "/usr";
				}
				else
				{
					//Vista: C:\ProgramData
					//XP: c:\Program Files\Common Files				
					//XP = 5.1 & Vista = 6.0
					if (Environment.OSVersion.Version.Major >= 6)
					{
						path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
					}
					else
					{
						path = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
					}
				}
				return path;
			}
		}

        /// <summary>
        /// If we are runnign program from debugger, all protocol Add-Ins are loaded from child "Protocols"- directory. 
        /// </summary>
		public static string ProtocolAddInsPath
		{
			get
			{
				string strPath = "";
				if (Environment.OSVersion.Platform == PlatformID.Unix)
				{					
					strPath = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
					strPath = System.IO.Path.Combine(strPath, ".Gurux");
				}
				else
				{				
	                if (Environment.OSVersion.Version.Major < 6)
					{
						strPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
					}
					else
					{
						strPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
					}
					strPath = System.IO.Path.Combine(strPath, "Gurux");
				}                
                strPath = Path.Combine(strPath, "AddIns");
				return strPath;
			}
		}

		/// <summary>
		/// Retrieves application data path from environment variables.
		/// </summary>
		public static string UserDataPath
		{
			get
			{
				string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				return path;
			}
		}

		/// <summary>
		/// Shows an error message.
		/// </summary>
		public static void ShowError(IWin32Window parent, string title, Exception ex)
		{
			try
			{
				while (ex.InnerException != null)
				{
					ex = ex.InnerException;
				}

				if (ex.StackTrace != null)
				{
					Gurux.Common.GXCommon.TraceWrite(ex.StackTrace.ToString());
				}
                string path = ApplicationDataPath;
				if (System.Environment.OSVersion.Platform == PlatformID.Unix)
				{
					path = Path.Combine (path, ".Gurux");
				}
				else
				{
					path = Path.Combine (path, "Gurux");
				}
                path = Path.Combine(path, "LastError.txt");
                System.IO.TextWriter tw = System.IO.File.CreateText(path);
				tw.Write(ex.ToString());
                if (ex.StackTrace != null)
                {
                    tw.Write("----------------------------------------------------------\r\n");
                    tw.Write(ex.StackTrace.ToString());
                }
				tw.Close();
				if (parent != null && !((Control)parent).IsDisposed && !((Control)parent).InvokeRequired)
				{
					MessageBox.Show(parent, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else
				{
					MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
			catch
			{
				//Do nothing. Fatal exception blew up messagebox.
			}
		}

		/// <summary>
		/// Shows an error message.
		/// </summary>
		public static void ShowError(Exception ex)
		{
			ShowError(Owner, Title, ex);
		}

		/// <summary>
		/// Shows an error question dialog.
		/// </summary>
		public static DialogResult ShowQuestion(string str)
		{
			return ShowQuestion(Owner, Title, str);
		}

		/// <summary>
		/// Shows an error exclamation dialog.
		/// </summary>
		public static DialogResult ShowExclamation(string str)
		{
			return ShowExclamation(Owner, Title, str);
		}

		/// <summary>
		/// Shows an error message.
		/// </summary>
		public static void ShowError(IWin32Window parent, Exception ex)
		{
			ShowError(parent, Title, ex);
		}

		/// <summary>
		/// Shows an error question dialog.
		/// </summary>
		public static DialogResult ShowQuestion(IWin32Window parent, string str)
		{
			return ShowQuestion(parent, Title, str);
		}

		/// <summary>
		/// Shows an error exclamation dialog.
		/// </summary>
		public static DialogResult ShowExclamation(IWin32Window parent, string str)
		{
			return ShowExclamation(parent, Title, str);
		}


		/// <summary>
		/// Shows an error question dialog.
		/// </summary>
		public static DialogResult ShowQuestion(IWin32Window parent, string title, string str)
		{
			try
			{
				if (Environment.UserInteractive)
				{
					if (parent != null)
					{
						return MessageBox.Show(parent, str, title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
					}
					else
					{
						return MessageBox.Show(str, title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
					}
				}
				else
				{
					return DialogResult.Yes;
				}
			}
			catch
			{
				//Do nothing. Fatal exception blew up messagebox.
				return DialogResult.Abort;
			}
		}

		/// <summary>
		/// Shows an error exclamation dialog.
		/// </summary>
		public static DialogResult ShowExclamation(IWin32Window parent, string title, string str)
		{
			try
			{
				if (parent != null)
				{
					return MessageBox.Show(parent, str, title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);
				}
				else
				{
					return MessageBox.Show(str, title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);
				}
			}
			catch
			{
				//Do nothing. Fatal exception blew up messagebox.
				return DialogResult.Abort;
			}
		}

		/// <summary>
		/// Removes case sensitivity of given string.
		/// </summary>
        /// <param name="original">Original string.</param>
        /// <param name="pattern">String to replace.</param>
        /// <param name="replacement">Replacing string.</param>
        /// <returns>The replaced string.</returns>
		public static string ReplaceEx(string original, string pattern, string replacement)
		{
			int count, position0, position1;
			count = position0 = position1 = 0;
			string upperString = original.ToUpper();
			string upperPattern = pattern.ToUpper();
			int inc = (original.Length / pattern.Length) *
				(replacement.Length - pattern.Length);
			char[] chars = new char[original.Length + Math.Max(0, inc)];
			while ((position1 = upperString.IndexOf(upperPattern,
				position0)) != -1)
			{
				for (int i = position0; i < position1; ++i)
					chars[count++] = original[i];
				for (int i = 0; i < replacement.Length; ++i)
					chars[count++] = replacement[i];
				position0 = position1 + pattern.Length;
			}
			if (position0 == 0) return original;
			for (int i = position0; i < original.Length; ++i)
				chars[count++] = original[i];
			return new string(chars, 0, count);
		}
	}
}