/*
 * Copyright (C) 2004, 2005 Jorn Baayen <jbaayen@gnome.org>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation; either version 2 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public
 * License along with this program; if not, write to the
 * Free Software Foundation, Inc., 59 Temple Place - Suite 330,
 * Boston, MA 02111-1307, USA.
 */

// TODO: Split off WatchedFolders stuff.

using System;
using System.Collections;
using System.IO;

namespace Muine
{
	public class SongDatabase 
	{
		// GConf
		// 	MCS doesn't support array constants yet (as of 1.0)
		private const string GConfKeyWatchedFolders = "/apps/muine/watched_folders";
		private readonly string [] GConfDefaultWatchedFolders = new string [0];

		// Events
		public delegate void SongAddedHandler (Song song);
		public event SongAddedHandler SongAdded;

		public delegate void SongChangedHandler (Song song);
		public event SongChangedHandler SongChanged;

		public delegate void SongRemovedHandler (Song song);
		public event SongRemovedHandler SongRemoved;

		public delegate void AlbumAddedHandler (Album album);
		public event AlbumAddedHandler AlbumAdded;

		public delegate void AlbumChangedHandler (Album album);
		public event AlbumChangedHandler AlbumChanged;
		
		public delegate void AlbumRemovedHandler (Album album);
		public event AlbumRemovedHandler AlbumRemoved;

		// Internal Classes
		// Internal Classes :: BooleanBox
		private class BooleanBox {
			// Fields
			public bool Value;

			// Constructor
			public BooleanBox (bool val)
			{
				Value = val;
			}
		}

		// Internal Classes :: SignalRequest
		private class SignalRequest {
			// Fields
			public Song Song;

			public bool SongAdded   = false;
			public bool SongChanged = false;
			public bool SongRemoved = false;

			public Album AddedAlbum   = null;
			public Album RemovedAlbum = null;

			public Album AddChangedAlbum    = null;
			public Album RemoveChangedAlbum = null;

			public bool AlbumSongsChanged;

			// Constructor
			public SignalRequest (Song song)
			{
				Song = song;
			}
		}

		// Internal Classes :: AddFoldersThread
		//	TODO: Split off?
		private class AddFoldersThread : ThreadBase
		{
			// Objects
			private ProgressWindow pw;
			private BooleanBox canceled_box = new BooleanBox (false);
			
			// Variables
			private ArrayList folders;
			private DirectoryInfo current_folder;
			
			// Constructor
			public AddFoldersThread (ArrayList folders)
			{
				this.folders = folders;

				pw = new ProgressWindow (Global.Playlist);

				current_folder = (DirectoryInfo) folders [0];
				pw.Report (current_folder.Name, current_folder.Name);

				thread.Start ();
			}

			// Delegate Functions
			// Delegate Functions :: ThreadFunc
			protected override void ThreadFunc ()
			{
				foreach (DirectoryInfo dinfo in folders) {
					current_folder = dinfo;

					Global.DB.HandleDirectory (dinfo, queue, canceled_box);
				}

				thread_done = true;
			}
			
			// Delegate Functions :: MainLoopIdle
			protected override bool MainLoopIdle ()
			{
				if (queue.Count == 0) {
					if (thread_done) {
						pw.Done ();
						return false;
					} else {
						return true;
					}
				}

				SignalRequest rq = (SignalRequest) queue.Dequeue ();

				canceled_box.Value = pw.Report (current_folder.Name,
				                                Path.GetFileName (rq.Song.Filename));

				Global.DB.HandleSignalRequest (rq);
	
				return true;
			}
		}

		// Internal Classes :: CheckChangesThread
		//	TODO: Split off?
		private class CheckChangesThread : ThreadBase
		{
			// Constructor
			public CheckChangesThread ()
			{
				thread.Start ();
			}

			// Delegate Functions
			// Delegate Functions :: MainLoopIdle
			protected override bool MainLoopIdle ()
			{
				if (queue.Count == 0)
					return !thread_done;

				SignalRequest rq = (SignalRequest) queue.Dequeue ();

				Global.DB.HandleSignalRequest (rq);

				return true;
			}

			// Delegate Functions :: ThreadFunc
			protected override void ThreadFunc ()
			{
				Hashtable snapshot;
				lock (Global.DB)
					snapshot = (Hashtable) Global.DB.Songs.Clone ();

				// check for removed songs and changes
				foreach (string file in snapshot.Keys) {
					FileInfo finfo = new FileInfo (file);
					Song song = (Song) snapshot [file];

					SignalRequest rq = null;

					if (!finfo.Exists) {
						rq = Global.DB.StartRemoveSong (song);

					} else if (FileUtils.MTimeToTicks (song.MTime) < finfo.LastWriteTimeUtc.Ticks) {
						try {
							Metadata metadata = new Metadata (song.Filename);
							rq = Global.DB.StartSyncSong (song, metadata);

						} catch {
							try {
								rq = Global.DB.StartRemoveSong (song);
							} catch (InvalidOperationException e) {}
						}
					}

					if (rq != null)
						queue.Enqueue (rq);
				}

				// check for new songs
				foreach (string folder in Global.DB.WatchedFolders) {
					DirectoryInfo dinfo = new DirectoryInfo (folder);
					if (!dinfo.Exists)
						continue;

					BooleanBox canceled = new BooleanBox (false);
					Global.DB.HandleDirectory (dinfo, queue, canceled);
				}

				thread_done = true;
			}
		}

		// Objects
		private Database db;

		// Variables
		private Hashtable songs;
		private Hashtable albums;
		private string [] watched_folders;

		// Properties
		// 	When iterating Song or Albums of these don't forget to 
		//	lock the DB, otherwise the hash might be changed by another 
		//	thread while iterating.
		// Properties :: Songs (get;)
		public Hashtable Songs {
			get { return songs; }
		}

		// Properties :: Albums (get;)
		public Hashtable Albums {
			get { return albums; }
		}

		// Properties :: WatchedFolders (get;)
		public string [] WatchedFolders {
			get { return watched_folders; }
		}

		// Constructor
		public SongDatabase (int version)
		{
			db = new Database (FileUtils.SongsDBFile, version);
			
			songs = new Hashtable ();
			albums = new Hashtable ();

			watched_folders = (string []) Config.Get (GConfKeyWatchedFolders, GConfDefaultWatchedFolders);
			Config.AddNotify (GConfKeyWatchedFolders,
					  new GConf.NotifyEventHandler (OnWatchedFoldersChanged));
		}

		// Methods
		// Methods :: Public
		// Methods :: Public :: Load
		public void Load ()
		{
			lock (this)
				db.Load (new Database.DecodeFunctionDelegate (DecodeFunction));
		}

		// Methods :: Public :: AddSong
		public void AddSong (Song song)
		{
			SignalRequest rq;
			try {
				rq = StartAddSong (song);
				HandleSignalRequest (rq);

			} catch (InvalidOperationException e) {
				return;
			}
		}

		// Methods :: Public :: SaveSong
		public void SaveSong (Song song)
		{
			lock (this)
				SaveSongInternal (song, true);
		}

		// Methods :: Public :: RemoveSong
		public void RemoveSong (Song song)
		{
			SignalRequest rq;
			try {
				rq = StartRemoveSong (song);
				HandleSignalRequest (rq);

			} catch (InvalidOperationException e) {
				return;
			}
		}

		// Methods :: Public :: AddFolders
		public void AddFolders (ArrayList folders)
		{
			foreach (DirectoryInfo dinfo in folders)
				AddToWatchedFolders (dinfo.FullName);
			Config.Set (GConfKeyWatchedFolders, watched_folders);

			new AddFoldersThread (folders);
		}

		// Methods :: Public :: CheckChanges
		public void CheckChanges ()
		{
			new CheckChangesThread ();
		}

		/*
		The album key is "folder:album name" because of the following
		reasons:
		
			We cannot do artist/performer matching, because it is 
		very common for albums to be made by different artists. Random 
		example, the Sigur Rós/Radiohead split. Using "Various Artists"
		as artist tag is whacky.
		
			But, we cannot match only by album name either: a user
		may very well have multiple albums with the title "Greatest 
		Hits". We don't want to	incorrectly group all these together.
		
			So, the best thing we've managed to come up with so far
		is using "folder:albumname". This because most people who even 
		have whole albums have those organised in folders, or at the 
		very least all music files in the same folder. So for those it 
		should more or less work. And for those who have a decently 
		organised music collection, the original target user base, it 
		should work flawlessly. And for those who have a REALLY poorly
		organised collection, well, bummer. Moving all files to the 
		same dir will help a bit.
		*/
		public string MakeAlbumKey (string folder, string album_name)
		{
			return String.Format ("{0}:{1}", folder, album_name.ToLower ());
		}

		// Methods :: Public :: Getters :: GetSong
		public Song GetSong (string filename)
		{
			return (Song) Songs [filename];
		}

		// Methods :: Public :: Getters :: GetAlbum
		public Album GetAlbum (Song song)
		{
			return GetAlbum (song.AlbumKey);
		}

		public Album GetAlbum (string key)
		{
			return (Album) Albums [key];
		}
							
		// Methods :: Private
		// Methods :: Private :: StartAddSong
		private SignalRequest StartAddSong (Song song)
		{
			lock (this) {
				SignalRequest rq = new SignalRequest (song);
			
				try {
					Songs.Add (song.Filename, song);
				} catch (ArgumentException e) { // already exists
					throw new InvalidOperationException ();
				}

				StartAddToAlbum (rq);

				// Store after the album cover has been stored,
				// in case of unexpected exit
				SaveSongInternal (song, false);

				rq.SongAdded = true;

				return rq;
			}
		}

		// Methods :: Private :: StartSyncSong
		private SignalRequest StartSyncSong (Song song, Metadata metadata)
		{
			lock (this) {
				if (song.Dead)
					throw new InvalidOperationException ();

				SignalRequest rq = new SignalRequest (song);
			
				StartRemoveFromAlbum (rq);
				song.Sync (metadata);
				StartAddToAlbum (rq);
			
				SaveSongInternal (song, true);

				rq.SongChanged = true;
				
				return rq;
			}
		}

		// Methods :: Private :: SaveSongInternal
		private void SaveSongInternal (Song song, bool overwrite)
		{
			int data_size;
			IntPtr data = song.Pack (out data_size);
			db.Store (song.Filename, data, data_size, overwrite);
		}

		// Methods :: Private :: StartRemoveSong
		private SignalRequest StartRemoveSong (Song song)
		{
			lock (this) {
				if (song.Dead)
					throw new InvalidOperationException ();

				SignalRequest rq = new SignalRequest (song);

				db.Delete (song.Filename);

				Songs.Remove (rq.Song.Filename);

				StartRemoveFromAlbum (rq);

				rq.SongRemoved = true;

				return rq;
			}
		}

		// Methods :: Private :: AddToAlbum
		private void AddToAlbum (Song song)
		{
			SignalRequest rq = new SignalRequest (song);
			
			StartAddToAlbum (rq);
			HandleSignalRequest (rq);
		}

		// Methods :: Private :: StartAddToAlbum
		private void StartAddToAlbum (SignalRequest rq)
		{
			StartAddToAlbum (rq, null);
		}

		private void StartAddToAlbum (Song song)
		{
			StartAddToAlbum (null, song);
		}

		private void StartAddToAlbum (SignalRequest rq, Song s)
		{
			bool from_db = (s != null);
			
			Song song = (from_db)
				     ? s
				     : rq.Song;
			
			if (!song.HasAlbum)
				return;

			string key = song.AlbumKey;

			Album album = (Album) Albums [key];
			
			bool changed = false;
			bool added = false;
			bool songs_changed = false;

			if (album == null) {
				album = new Album (song, !from_db);
				Albums.Add (key, album);

				added = true;
			} else {
				album.Add (song,
					   !from_db,
				           out changed,
					   out songs_changed);
			}

			if (!from_db) {
				if (added)
					rq.AddedAlbum = album;
				else if (changed)
					rq.AddChangedAlbum = album;
					
				rq.AlbumSongsChanged = songs_changed;
			}
		}

		// Methods :: Private :: StartRemoveFromAlbum
		private void StartRemoveFromAlbum (SignalRequest rq)
		{
			if (!rq.Song.HasAlbum)
				return;

			string key = rq.Song.AlbumKey;

			Album album = (Album) Albums [key];
			if (album == null)
				return;
				
			bool changed, empty;
			album.Remove (rq.Song, out changed, out empty);

			if (empty) {
				Albums.Remove (key);

				rq.RemovedAlbum = album;
			} else if (changed) {
				rq.RemoveChangedAlbum = album;
			}
		}

		// Methods :: Private :: AddToWatchedFolders
		private void AddToWatchedFolders (string folder)
		{
			ArrayList new_folders = new ArrayList ();

			foreach (string cur in watched_folders) {
				if (folder.IndexOf (cur) == 0 &&
				    folder.Length >= cur.Length) {
					// folder is already monitored at a
					// higher or same level, don't add
					return;
				} else if (cur.IndexOf (folder) == 0 &&
				           folder.Length < cur.Length) {
				        // we are now adding a lower level
					// than 'cur', so don't add 'cur' to the
					// new array.
					continue;
				}

				new_folders.Add (cur);
			}

			new_folders.Add (folder);

			watched_folders = (string []) new_folders.ToArray (typeof (string));
		}

		// Methods :: Private :: HandleDirectory
		// 	Directory walking
		private bool HandleDirectory (DirectoryInfo info, Queue queue, BooleanBox canceled_box)
		{
			FileInfo [] finfos;
			
			try {
				finfos = info.GetFiles ();
			} catch {
				return true;
			}

			foreach (FileInfo finfo in finfos) {
				if (canceled_box.Value)
					return false;

				if (Songs [finfo.FullName] == null) {
					Song song;

					try {
						song = new Song (finfo.FullName);
					} catch {
						continue;
					}
	
					SignalRequest rq;				
					try {
						rq = StartAddSong (song);
					} catch (InvalidOperationException e) {
						continue;
					}

					queue.Enqueue (rq);
				}
			}

			DirectoryInfo [] dinfos;
			
			try {
				dinfos = info.GetDirectories ();
			} catch {
				return true;
			}

			foreach (DirectoryInfo dinfo in dinfos) {
				bool ret = HandleDirectory (dinfo, queue, canceled_box);
				if (!ret)
					return false;
			}

			return true;
		}

		// Methods :: Private :: HandleSignalRequest
		private void HandleSignalRequest (SignalRequest rq)
		{
			lock (this) {
				if (rq.Song.Dead)
					return;

				// Song
				if (rq.SongAdded) {
					EmitSongAdded (rq.Song);

				} else if (rq.SongChanged) {
					EmitSongChanged (rq.Song);

				} else if (rq.SongRemoved) {
					EmitSongRemoved (rq.Song);
					rq.Song.Deregister ();
				}
				
				// Albums
				if (rq.AddedAlbum != null) {
					EmitAlbumAdded (rq.AddedAlbum);
				}
				
				if (rq.RemovedAlbum != null) {
					EmitAlbumRemoved (rq.RemovedAlbum);
				}

				if (rq.AddChangedAlbum != null) {
					EmitAlbumChanged (rq.AddChangedAlbum);

					if (rq.AlbumSongsChanged)
						foreach (Song s in rq.AddChangedAlbum.Songs)
							EmitSongChanged (s);

				}

				if (rq.RemoveChangedAlbum != null) {
					EmitAlbumChanged (rq.RemoveChangedAlbum);
				}
			}
		}

		// Methods :: Private :: Signal Emitters
		// Methods :: Private :: Signal Emitters :: EmitSongAdded
		private void EmitSongAdded (Song song)
		{
			if (SongAdded != null)
				SongAdded (song);
		}

		// Methods :: Private :: Signal Emitters :: EmitSongChanged
		public void EmitSongChanged (Song song)
		{
			if (SongChanged != null)
				SongChanged (song);
		}

		// Methods :: Private :: Signal Emitters :: EmitSongRemoved
		private void EmitSongRemoved (Song song)
		{
			if (SongRemoved != null)
				SongRemoved (song);
		}

		// Methods :: Private :: Signal Emitters :: EmitAlbumAdded
		private void EmitAlbumAdded (Album album)
		{
			if (AlbumAdded != null)
				AlbumAdded (album);
		}

		// Methods :: Private :: Signal Emitters :: EmitAlbumChanged
		public void EmitAlbumChanged (Album album)
		{
			if (AlbumChanged != null)
				AlbumChanged (album);
		}

		// Methods :: Private :: Signal Emitters :: EmitAlbumRemoved
		private void EmitAlbumRemoved (Album album)
		{
			if (AlbumRemoved != null)
				AlbumRemoved (album);
		}

		// Handlers
		// Handlers :: OnWatchedFoldersChanged
		private void OnWatchedFoldersChanged (object o, GConf.NotifyEventArgs args)
		{
			string [] old_watched_folders = watched_folders;
			watched_folders = (string []) args.Value;

			ArrayList new_dinfos = new ArrayList ();

			Array.Sort (old_watched_folders); // Needed for the binary search

			foreach (string s in watched_folders) {
				if (Array.BinarySearch (old_watched_folders, s) < 0) {
					DirectoryInfo dinfo = new DirectoryInfo (s);
					if (!dinfo.Exists)
						continue;

					new_dinfos.Add (dinfo);
				}
			}

			if (new_dinfos.Count > 0)
				new AddFoldersThread (new_dinfos);
		}

		// Delegate Functions
		// Delegate Functions :: DecodeFunction
		private void DecodeFunction (string key, IntPtr data)
		{
			Song song = new Song (key, data);

			Songs.Add (key, song);
			
			// we don't "Finish", as we do this before the UI is there,
			// we don't need to emit signals
			StartAddToAlbum (song);
		}
	}
}
