using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Collections.ObjectModel;


//Encapsulates access to alternative data streams of an NTFS file.
//Adapted from a C++ sample by Dino Esposito,
//http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dnfiles/html/ntfs5.asp
// see also http://msdn.microsoft.com/en-us/magazine/cc163677.aspx

namespace NewsComponents.Utils
{
    /// <summary>
    /// Wraps the API functions, structures and constants.
    /// </summary>
    internal static class NativeMethods
    {
        public const char STREAM_SEP = ':';
        public const int INVALID_HANDLE_VALUE = -1;
        public const int MAX_PATH = 256;

        [Flags]
        public enum FileFlags : uint
        {
            WriteThrough = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x8000000,
            DeleteOnClose = 0x4000000,
            BackupSemantics = 0x2000000,
            PosixSemantics = 0x1000000,
            OpenReparsePoint = 0x200000,
            OpenNoRecall = 0x100000
        }

        [Flags]
        public enum FileAccessAPI : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000
        }

        /// <summary>
        /// Provides a mapping between a System.IO.FileAccess value and a FileAccessAPI value.
        /// </summary>
        /// <param name="Access">The <see cref="System.IO.FileAccess"/> value to map.</param>
        /// <returns>The <see cref="FileAccessAPI"/> value.</returns>
        public static FileAccessAPI Access2API(FileAccess Access)
        {
            FileAccessAPI lRet = 0;
            if ((Access & FileAccess.Read) == FileAccess.Read) lRet |= FileAccessAPI.GENERIC_READ;
            if ((Access & FileAccess.Write) == FileAccess.Write) lRet |= FileAccessAPI.GENERIC_WRITE;
            return lRet;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LARGE_INTEGER
        {
            public int Low;
            public int High;

            public long ToInt64()
            {
                return High*4294967296L + Low;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WIN32_STREAM_ID
        {
            public int dwStreamID;
            public int dwStreamAttributes;
            //public LARGE_INTEGER Size;
            public long Size;
            public int dwStreamNameSize;
        }

		[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern SafeFileHandle CreateFile(
			string filename, 
			[MarshalAs(UnmanagedType.U4)] FileAccessAPI access, 
			[MarshalAs(UnmanagedType.U4)] FileShare share,
			IntPtr securityAttributes,						// optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
			[MarshalAs(UnmanagedType.U4)] FileMode creation,
			[MarshalAs(UnmanagedType.U4)] FileFlags flagsAndAttributes, 
			IntPtr templateFile);

		[DllImport("kernel32", CharSet = CharSet.Unicode)]
        public static extern bool DeleteFile(string name);


        [DllImport("kernel32")]
        public static extern bool BackupRead(SafeFileHandle hFile, IntPtr pBuffer, uint lBytes, ref uint lRead, bool bAbort,
                                             bool bSecurity, ref IntPtr context);

        [DllImport("kernel32")]
        public static extern bool BackupRead(SafeFileHandle hFile, ref WIN32_STREAM_ID pBuffer, uint lBytes, ref uint lRead,
											 bool bAbort, bool bSecurity, ref IntPtr context);

        [DllImport("kernel32")]
        public static extern bool BackupSeek(SafeFileHandle hFile, uint dwLowBytesToSeek, uint dwHighBytesToSeek, ref uint dwLow,
                                             ref uint dwHigh, ref IntPtr context);
    }

    /// <summary>
    /// Encapsulates a single alternative data stream for a file.
    /// </summary>
    public class StreamInfo : IEquatable<StreamInfo>
    {
        private readonly FileStreams _parent;
        private readonly string _name;
        private readonly long _size;

        internal StreamInfo(FileStreams Parent, string Name, long Size)
        {
            _parent = Parent;
            _name = Name;
            _size = Size;
        }

        /// <summary>
        /// The name of the stream.
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
        }

        /// <summary>
        /// The size (in bytes) of the stream.
        /// </summary>
        public long Size
        {
            get
            {
                return _size;
            }
        }

		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
		/// </returns>
        public override string ToString()
        {
            return String.Format("{1}{0}{2}{0}$DATA", NativeMethods.STREAM_SEP, _parent.FileName, _name);
        }

		/// <summary>
		/// Equalses the specified o.
		/// </summary>
		/// <param name="o">The o.</param>
		/// <returns></returns>
        public override bool Equals(Object o)
		{
			if (o is string)
            {
                return ((string)o).Equals(ToString());
            }

			return Equals(o as StreamInfo);
		}

		/// <summary>
		/// Serves as a hash function for a particular type.
		/// </summary>
		/// <returns>
		/// A hash code for the current <see cref="T:System.Object"/>.
		/// </returns>
    	public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        #region Open

        /// <summary>
        /// Opens or creates the stream in read-write mode, with no sharing.
        /// </summary>
        /// <returns>A <see cref="System.IO.FileStream"/> wrapper for the stream.</returns>
        public FileStream Open()
        {
            return Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// Opens or creates the stream in read-write mode with no sharing.
        /// </summary>
        /// <param name="Mode">The <see cref="System.IO.FileMode"/> action for the stream.</param>
        /// <returns>A <see cref="System.IO.FileStream"/> wrapper for the stream.</returns>
        public FileStream Open(FileMode Mode)
        {
            return Open(Mode, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// Opens or creates the stream with no sharing.
        /// </summary>
        /// <param name="Mode">The <see cref="System.IO.FileMode"/> action for the stream.</param>
        /// <param name="Access">The <see cref="System.IO.FileAccess"/> level for the stream.</param>
        /// <returns>A <see cref="System.IO.FileStream"/> wrapper for the stream.</returns>
        public FileStream Open(FileMode Mode, FileAccess Access)
        {
            return Open(Mode, Access, FileShare.None);
        }

        /// <summary>
        /// Opens or creates the stream.
        /// </summary>
        /// <param name="Mode">The <see cref="System.IO.FileMode"/> action for the stream.</param>
        /// <param name="Access">The <see cref="System.IO.FileAccess"/> level for the stream.</param>
        /// <param name="Share">The <see cref="System.IO.FileShare"/> level for the stream.</param>
        /// <returns>A <see cref="System.IO.FileStream"/> wrapper for the stream.</returns>
        public FileStream Open(FileMode Mode, FileAccess Access, FileShare Share)
        {
            try
            {
				SafeFileHandle hFile = NativeMethods.CreateFile(ToString(), NativeMethods.Access2API(Access), Share, IntPtr.Zero, Mode, 0, IntPtr.Zero);
                return new FileStream(hFile, Access);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Delete

        /// <summary>
        /// Deletes the stream from the file.
        /// </summary>
        /// <returns>A <see cref="System.Boolean"/> value: true if the stream was deleted, false if there was an error.</returns>
        public bool Delete()
        {
            return NativeMethods.DeleteFile(ToString());
        }

        #endregion

        #region IEquatable<StreamInfo> Members

		/// <summary>
		/// Indicates whether the current object is equal to another object of the same type.
		/// </summary>
		/// <param name="other">An object to compare with this object.</param>
		/// <returns>
		/// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
		/// </returns>
        public bool Equals(StreamInfo other)
        {
            if(ReferenceEquals(other, null))
                return false;
            

            return (other._name.Equals(_name) && other._parent.Equals(_parent));
            
            
        }

        #endregion
    }


    /// <summary>
    /// Encapsulates the collection of alternative data streams for a file.
    /// A collection of <see cref="StreamInfo"/> objects.
    /// </summary>
    public class FileStreams : Collection<StreamInfo>
    {
        private readonly FileInfo _file;

        #region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="FileStreams"/> class.
		/// </summary>
		/// <param name="File">The file.</param>
        public FileStreams(string File)
        {
            _file = new FileInfo(File);
            initStreams();
        }

		/// <summary>
		/// Initializes a new instance of the <see cref="FileStreams"/> class.
		/// </summary>
		/// <param name="file">The file.</param>
        public FileStreams(FileInfo file)
        {
            _file = file;
            initStreams();
        }

        /// <summary>
        /// Reads the streams from the file.
        /// </summary>
        private void initStreams()
        {
            //Open the file with backup semantics
            using (SafeFileHandle hFile =
                NativeMethods.CreateFile(_file.FullName, NativeMethods.FileAccessAPI.GENERIC_READ, FileShare.Read, IntPtr.Zero,
									FileMode.Open, NativeMethods.FileFlags.BackupSemantics, IntPtr.Zero))
            {
                if (hFile.IsInvalid) return;

                NativeMethods.WIN32_STREAM_ID sid = new NativeMethods.WIN32_STREAM_ID();
                uint dwStreamHeaderSize = (uint)Marshal.SizeOf(sid);
                IntPtr Context = IntPtr.Zero;
                bool Continue = true;
                while (Continue)
                {
                    //Read the next stream header
                    uint lRead = 0;
                    Continue =
                        NativeMethods.BackupRead(hFile, ref sid, dwStreamHeaderSize, ref lRead, false, false, ref Context);
                    if (Continue && lRead == dwStreamHeaderSize)
                    {
                        if (sid.dwStreamNameSize > 0)
                        {
                            //Read the stream name
                            lRead = 0;
                            IntPtr pName = Marshal.AllocHGlobal(sid.dwStreamNameSize);
                            try
                            {
                                NativeMethods.BackupRead(hFile, pName, (uint)sid.dwStreamNameSize, ref lRead, false, false,
                                                    ref Context);
                                char[] bName = new char[sid.dwStreamNameSize];
                                Marshal.Copy(pName, bName, 0, sid.dwStreamNameSize);

                                //Name is of the format ":NAME:$DATA\0"
                                string sName = new string(bName);
                                int i = sName.IndexOf(NativeMethods.STREAM_SEP, 1);
                                if (i > -1) sName = sName.Substring(1, i - 1);
                                else
                                {
                                    //This should never happen. 
                                    //Truncate the name at the first null char.
                                    i = sName.IndexOf('\0');
                                    if (i > -1) sName = sName.Substring(1, i - 1);
                                }

                                //Add the stream to the collection
                                Items.Add(new StreamInfo(this, sName, sid.Size));
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(pName);
                            }
                        }

                        //Skip the stream contents
                        uint l = 0;
                        uint h = 0;
                        Continue =
							NativeMethods.BackupSeek(hFile, GetLowWord(sid.Size), GetHighWord(sid.Size), ref l, ref h, ref Context);
                    }
                    else break;
                }
            }
        }

	    internal uint GetLowWord(long value)
	    {
		    return (uint) ((value << 32) >> 32);
	    }
		internal uint GetHighWord(long value)
		{
			return (uint)(value >> 32);
		}

        #endregion

        #region File

        /// <summary>
        /// Returns the <see cref="System.IO.FileInfo"/> object for the wrapped file. 
        /// </summary>
        public FileInfo FileInfo
        {
            get
            {
                return _file;
            }
        }

        /// <summary>
        /// Returns the full path to the wrapped file.
        /// </summary>
        public string FileName
        {
            get
            {
                return _file.FullName;
            }
        }

        /// <summary>
        /// Returns the size of the main data stream, in bytes.
        /// </summary>
        public long FileSize
        {
            get
            {
                return _file.Length;
            }
        }

        /// <summary>
        /// Returns the size of all streams for the file, in bytes.
        /// </summary>
        public long Size
        {
            get
            {
                long size = this.FileSize;
                foreach (StreamInfo s in this) size += s.Size;
                return size;
            }
        }

        #endregion

        #region Open

        /// <summary>
        /// Opens or creates the default file stream.
        /// </summary>
        /// <returns><see cref="System.IO.FileStream"/></returns>
        public FileStream Open()
        {
            return new FileStream(_file.FullName, FileMode.OpenOrCreate);
        }

        /// <summary>
        /// Opens or creates the default file stream.
        /// </summary>
        /// <param name="Mode">The <see cref="System.IO.FileMode"/> for the stream.</param>
        /// <returns><see cref="System.IO.FileStream"/></returns>
        public FileStream Open(FileMode Mode)
        {
            return new FileStream(_file.FullName, Mode);
        }

        /// <summary>
        /// Opens or creates the default file stream.
        /// </summary>
        /// <param name="Mode">The <see cref="System.IO.FileMode"/> for the stream.</param>
        /// <param name="Access">The <see cref="System.IO.FileAccess"/> for the stream.</param>
        /// <returns><see cref="System.IO.FileStream"/></returns>
        public FileStream Open(FileMode Mode, FileAccess Access)
        {
            return new FileStream(_file.FullName, Mode, Access);
        }

        /// <summary>
        /// Opens or creates the default file stream.
        /// </summary>
        /// <param name="Mode">The <see cref="System.IO.FileMode"/> for the stream.</param>
        /// <param name="Access">The <see cref="System.IO.FileAccess"/> for the stream.</param>
        /// <param name="Share">The <see cref="System.IO.FileShare"/> for the stream.</param>
        /// <returns><see cref="System.IO.FileStream"/></returns>
        public FileStream Open(FileMode Mode, FileAccess Access, FileShare Share)
        {
            return new FileStream(_file.FullName, Mode, Access, Share);
        }

        #endregion

        #region Delete

        /// <summary>
        /// Deletes the file, and all alternative streams.
        /// </summary>
        public void Delete()
        {
            for (int i = Items.Count; i > 0; i--)
            {
                Items.RemoveAt(i);
            }
            _file.Delete();
        }

        #endregion

        #region Collection operations

        /// <summary>
        /// Add an alternative data stream to this file.
        /// </summary>
        /// <param name="Name">The name for the stream.</param>
        /// <returns>The index of the new item.</returns>
        public void Add(string Name)
        {
            StreamInfo FSI = new StreamInfo(this, Name, 0);
            int i = Items.IndexOf(FSI);
            if (i == -1) 
                Items.Add(FSI);
        }

        /// <summary>
        /// Removes the alternative data stream with the specified name.
        /// </summary>
        /// <param name="Name">The name of the string to remove.</param>
        public void Remove(string Name)
        {
            StreamInfo FSI = new StreamInfo(this, Name, 0);
            int i = Items.IndexOf(FSI);
            if (i > -1) 
                Items.RemoveAt(i);
        }

        /// <summary>
        /// Returns the index of the <see cref="StreamInfo"/> object with the specified name in the collection.
        /// </summary>
        /// <param name="Name">The name of the stream to find.</param>
        /// <returns>The index of the stream, or -1.</returns>
        public int IndexOf(string Name)
        {
            return Items.IndexOf(new StreamInfo(this, Name, 0));
        }


		/// <summary>
		/// Gets the <see cref="NewsComponents.Utils.StreamInfo"/> with the specified name.
		/// </summary>
		/// <value></value>
        public StreamInfo this[string Name]
        {
            get
            {
                int i = IndexOf(Name);
                if (i == -1) return null;
            	return Items[i];
            }
        }

        #endregion

        #region Overrides


   

        /// <summary>
        /// Deletes the stream from the file when you remove it from the list.
        /// </summary>
        protected override void RemoveItem(int index)
        {
            StreamInfo fsi = this[index];
            base.RemoveItem(index);

            if (fsi != null)
                fsi.Delete();
        }


        #endregion
    }
}