/*
 * Copyright © 2004 Jorn Baayen <jorn@nl.linux.org>
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
 *
 * FIXME check file existance when loading db
 */

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

using Gnome;

public class SongDatabase 
{
	private IntPtr dbf;

	public Hashtable Songs; 

	public Hashtable Albums;

	private delegate void DecodeFuncDelegate (string key, IntPtr data, IntPtr user_data);
	
	[DllImport ("libmuine")]
	private static extern IntPtr db_open (string filename, out string error);
	[DllImport ("libmuine")]
	private static extern void db_foreach (IntPtr dbf, DecodeFuncDelegate decode_func,
					       IntPtr user_data);
						   
	public SongDatabase ()
	{
		DirectoryInfo dinfo = new DirectoryInfo (User.DirGet () + "/muine");
		if (!dinfo.Exists) {
			try {
				dinfo.Create ();
			} catch (Exception e) {
				throw e;
			}
		}
		
		string filename = dinfo.ToString () + "/songs.db";

		string error = null;

		dbf = db_open (filename, out error);

		if (dbf == IntPtr.Zero) {
			throw new Exception ("Failed to open database: " + error);
		}

		Songs = new Hashtable ();
		Albums = new Hashtable ();
	}

	public delegate void SongAddedHandler (Song song);
	public event SongAddedHandler SongAdded;

	public delegate void SongRemovedHandler (Song song);
	public event SongRemovedHandler SongRemoved;

	public delegate void AlbumAddedHandler (Album album);
	public event AlbumAddedHandler AlbumAdded;
	
	public delegate void AlbumRemovedHandler (Album album);
	public event AlbumRemovedHandler AlbumRemoved;

	public void Load ()
	{
		db_foreach (dbf, new DecodeFuncDelegate (DecodeFunc), IntPtr.Zero);
	}
	
	private void DecodeFunc (string key, IntPtr data, IntPtr user_data)
	{
		Song song = new Song (key, data);

		Muine.DB.Songs.Add (String.Copy (key), song);

		Muine.DB.DoAlbum (song, false);
	}

	private delegate IntPtr EncodeFuncDelegate (IntPtr handle, out int length);

	[DllImport ("libmuine")]
	private static extern void db_store (IntPtr dbf, string key, bool overwrite,
					     EncodeFuncDelegate encode_func,
					     IntPtr user_data);

	public void AddSong (Song song)
	{
		db_store (dbf, song.Filename, false,
		          new EncodeFuncDelegate (EncodeFunc), song.Handle);

		Songs.Add (song.Filename, song);

		DoAlbum (song, true);

		if (SongAdded != null)
			SongAdded (song);
	}

	[DllImport ("libmuine")]
	private static extern void db_delete (IntPtr dbf, string key);

	public void RemoveSong (Song song)
	{
		db_delete (dbf, song.Filename);

		if (SongRemoved != null)
			SongRemoved (song);

		if (song.Album.Length == 0)
			return;

		Album album = (Album) Albums [song.Album];
		if (album.RemoveSong (song)) {
			Albums.Remove (album.Name);

			if (AlbumRemoved != null)
				AlbumRemoved (album);
		}
	}

	private IntPtr EncodeFunc (IntPtr handle, out int length)
	{
		Song song = Song.FromHandle (handle);

		return song.Pack (out length);
	}

	public void UpdateSong (Song song)
	{
		db_store (dbf, song.Filename, true,
			  new EncodeFuncDelegate (EncodeFunc), song.Handle);
	}

	public void DoAlbum (Song song, bool emit_signal)
	{
		if (song.Album.Length == 0)
			return;

		Album album = (Album) Albums [song.Album];
		if (album == null) {
			album = new Album (song);
			Albums.Add (album.Name, album);

			if (emit_signal && AlbumAdded != null)
				AlbumAdded (album);
		} else {
			album.AddSong (song);
		}
	}

	public bool HaveFile (string filename)
	{
		return (Songs [filename] != null);
	}

	public Song SongFromFile (string filename)
	{
		return (Song) Songs [filename];
	}
}
