/*
 * Copyright (C) 2003, 2004, 2005 Jorn Baayen <jbaayen@gnome.org>
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

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Threading;

using Gdk;

using MuinePluginLib;

public class Song : ISong
{
	private string filename;
	public string Filename {
		get { return filename; }
	}
		
	private string title;
	public string Title {
		get { return title; }
	}

	private string [] artists;
	public string [] Artists {
		get { return artists; }
	}

	private string [] performers;
	public string [] Performers {
		get { return performers; }
	}

	private string album;
	public string Album {
		get { return album; }
	}

	private int track_number;
	public int TrackNumber {
		get { return track_number; }
	}

	private int disc_number;
	public int DiscNumber {
		get { return disc_number; }
	}

	private string year;
	public string Year {
		get { return year; }
	}

	private int duration;
	public int Duration {
		/* we have a setter too, because sometimes we want
		 * to correct the duration. */
		set { duration = value; }
		
		get { return duration; }
	}

	private Gdk.Pixbuf cover_image;
	public Gdk.Pixbuf CoverImage {
		set {
			cover_image = value;

			if (cover_image != null &&
			    cover_image != Muine.CoverDB.DownloadingPixbuf)
				CheckedCoverImage = true;
		}
		
		get { return cover_image; }
	}

	private int mtime;
	public int MTime {
		get { return mtime; }
	}

	private double gain;
	public double Gain {
		get { return gain; }
	}

	private double peak;
	public double Peak {
		get { return peak; }
	}

	private string sort_key = null;
	public string SortKey {
		get {
			if (sort_key == null) {
				string a = String.Join (" ", artists).ToLower ();
				string p = String.Join (" ", performers).ToLower ();
				
				sort_key = StringUtils.CollateKey (title.ToLower () + " " + a + " " + p);
			}
			
			return sort_key;
		}
	}

	private string search_key = null;
	public string SearchKey {
		get {
			if (search_key == null) {
				string a = String.Join (" ", artists).ToLower ();
				string p = String.Join (" ", performers).ToLower ();
				
				search_key = title.ToLower () + " " + a + " " + p + " " + album.ToLower ();
			}

			return search_key;
		}
	}

	/*
	- The album key is "dirname:album name" because of the following
	reasons: (I should add a comment in the code ..)
	We cannot do artist/performer matching, because it is very common for
	albums to be made by different artists. Random example, the Sigur
	Rós/Radiohead split. Using "Various Artists" as artist tag is whacky.
	But, we cannot match only by album name either: a user may very well
	have multiple albums with the title "Greatest Hits". We don't want to
	incorrectly group all these together.
	So, the best thing we've managed to come up with so far is using
	dirname:albumname. This because most people who even have whole albums
	have those organised in folders, or at the very least all music files in
	the same folder. So for those it should more or less work. And for those
	who have a decently organised music collection, the original target user
	base, it should work flawlessly. And for those who have a REALLY poorly
	organised collection, well, bummer. Moving all files to the same dir
	will help a bit.
	*/
	public string AlbumKey {
		get {
			if (album.Length == 0)
				return null;
				
			string dirname = Path.GetDirectoryName (filename);

			return dirname + ":" + album.ToLower ();
		}
	}

	private bool dead = false;
	public bool Dead {
		get { return dead; }
	}

	public void Kill ()
	{
		dead = true;

		pointers.Remove (handle);

		foreach (IntPtr extra_handle in handles)
			pointers.Remove (extra_handle);
	}

	private bool orphan = false;
	public bool Orphan {
		set { orphan = value; }
		
		get { return orphan; }
	}

	private static string [] cover_filenames = {
		"cover.jpg",
		"Cover.jpg",
		"cover.jpeg",
		"Cover.jpeg",
		"cover.png",
		"Cover.png",
		"folder.jpg",
		"Folder.jpg",
		"cover.gif",
		"Cover.gif"
	};

	private Gdk.Pixbuf tmp_cover_image;

	private bool dirty = false;
	public bool Dirty {
		set { dirty = value; }

		get { return dirty; }
	}

	private bool checked_cover_image;
	private bool CheckedCoverImage {
		set {
			if (checked_cover_image == value)
				return;

			checked_cover_image = value;

			dirty = true;
		}

		get { return checked_cover_image; }
	}

	/* this is run from the main thread */
	private bool ProcessDownloadedAlbumCover ()
	{
		if (dead)
			return false;

		if (checked_cover_image) {
			tmp_cover_image = null;

			return false;
		}

		CheckedCoverImage = true;

		cover_image = tmp_cover_image;
		tmp_cover_image = null;
		
		Muine.CoverDB.ReplaceCover (AlbumKey, cover_image);

		Muine.DB.UpdateSong (this);
		
		Muine.DB.SyncAlbumCoverImageWithSong (this);
		
		return false;
	}

	/* This is run from the action thread */
	private void DownloadAlbumCoverInThread (ActionThread.Action action)
	{
		try {
			tmp_cover_image = Muine.CoverDB.GetAlbumCoverFromAmazon (this);
		} catch (WebException e) {
			/* Temporary web problem (Timeout etc.) - re-queue */
			Thread.Sleep (60000); /* wait for a minute first */
			Muine.ActionThread.QueueAction (action);
			
			return;
		} catch (Exception e) {
			tmp_cover_image = null;
		}

		GLib.Idle.Add (new GLib.IdleHandler (ProcessDownloadedAlbumCover));
	}

	private string new_cover_url;

	private void DownloadAlbumCoverInThreadFromURL (ActionThread.Action action)
	{
		try {
			tmp_cover_image = Muine.CoverDB.DownloadCoverPixbuf (new_cover_url);
		} catch {
			tmp_cover_image = null;
		}

		CheckedCoverImage = false;

		GLib.Idle.Add (new GLib.IdleHandler (ProcessDownloadedAlbumCover));
	}

	public void DownloadNewCoverImage (string url)
	{
		new_cover_url = url;

		ActionThread.Action action = new ActionThread.Action (DownloadAlbumCoverInThreadFromURL);
		Muine.ActionThread.QueueAction (action);
	}

	private void GetCoverImage (Metadata metadata)
	{
		checked_cover_image = true;

		if (album.Length == 0) {
			cover_image = null;
			return;
		}

		string key = AlbumKey;

		/* Check the cache first */
		if (Muine.CoverDB.Covers.ContainsKey (key)) {
			cover_image = (Gdk.Pixbuf) Muine.CoverDB.Covers [key];
			return;
		}

		/* Search for popular image names */
		string dirname = Path.GetDirectoryName (filename);

		foreach (string fn in cover_filenames) {
			FileInfo cover = new FileInfo (dirname + "/" + fn);
			
			if (cover.Exists) {
				cover_image = Muine.CoverDB.AddCoverLocal (key, cover.ToString ());

				if (cover_image != null)
					return;
			}
		}

		/* Check for an embedded image in the ID3 tag */
		if (metadata != null && metadata.AlbumArt != null) {
			cover_image = Muine.CoverDB.AddCoverEmbedded (key, metadata.AlbumArt);

			if (cover_image != null)
				return;
		}

		if (artists.Length == 0) {
			cover_image = null;
			return;
		}

		cover_image = Muine.CoverDB.AddCoverDownloading (key);

		checked_cover_image = false;

		/* Failed to find a cover on disk - try the web */
		ActionThread.Action action = new ActionThread.Action (DownloadAlbumCoverInThread);
		Muine.ActionThread.QueueAction (action);
	}

	private IntPtr handle;
	public IntPtr Handle {
		get { return handle; }
	}

	private static Hashtable pointers = Hashtable.Synchronized (new Hashtable ());
	private static IntPtr cur_ptr = IntPtr.Zero;

	private ArrayList handles;

	/* support for having multiple handles to the same song,
	 * used for, for example, having the same song in the playlist
	 * more than once.
	 */
	public IntPtr RegisterExtraHandle ()
	{
		cur_ptr = new IntPtr (((int) cur_ptr) + 1);
		pointers [cur_ptr] = this;

		handles.Add (cur_ptr);

		return cur_ptr;
	}

	public bool IsExtraHandle (IntPtr h)
	{
		return ((pointers [h] == this) &&
		        (handle != h));
	}

	public ArrayList Handles {
		get { return handles; }
	}

	public void UnregisterExtraHandle (IntPtr handle)
	{
		handles.Remove (cur_ptr);

		pointers.Remove (handle);
	}

	public void Sync (Metadata metadata)
	{
		if (metadata.Title.Length > 0)
			title = metadata.Title;
		else
			title = Path.GetFileNameWithoutExtension (filename);
		
		artists = metadata.Artists;
		performers = metadata.Performers;
		album = metadata.Album;
		track_number = metadata.TrackNumber;
		disc_number = metadata.DiscNumber;
		year = metadata.Year;
		duration = metadata.Duration;
		mtime = metadata.MTime;
		gain = metadata.Gain;
		peak = metadata.Peak;

		sort_key = null;
		search_key = null;

		GetCoverImage (metadata);
		
		dirty = true;
	}

	public Song (string fn)
	{
		filename = fn;

		Metadata metadata;
			
		try {
			metadata = new Metadata (filename);
		} catch (Exception e) {
			throw e;
		}

		Sync (metadata);

		cur_ptr = new IntPtr (((int) cur_ptr) + 1);
		pointers [cur_ptr] = this;
		handle = cur_ptr;

		handles = new ArrayList ();
		handles.Add (cur_ptr);
	}

	public Song (string fn, IntPtr data)
	{
		IntPtr p = data;
		int len;

		filename = fn;

		p = Database.UnpackString (p, out title);

		p = Database.UnpackInt (p, out len);
		artists = new string [len];
		for (int i = 0; i < len; i++)
			p = Database.UnpackString (p, out artists [i]);

		p = Database.UnpackInt (p, out len);
		performers = new string [len];
		for (int i = 0; i < len; i++)
			p = Database.UnpackString (p, out performers [i]);

		p = Database.UnpackString (p, out album);
		p = Database.UnpackInt (p, out track_number);
		p = Database.UnpackInt (p, out disc_number);
		p = Database.UnpackString (p, out year);
		p = Database.UnpackInt (p, out duration);
		p = Database.UnpackInt (p, out mtime);
		p = Database.UnpackBool (p, out checked_cover_image);
		p = Database.UnpackDouble (p, out gain);
		p = Database.UnpackDouble (p, out peak);

		/* cover image is added later, when the covers are being loaded */

		cur_ptr = new IntPtr (((int) cur_ptr) + 1);
		pointers [cur_ptr] = this;
		handle = cur_ptr;

		handles = new ArrayList ();
		handles.Add (cur_ptr);

		if (!checked_cover_image)
			GetCoverImage (null);
	}

	public IntPtr Pack (out int length)
	{
		IntPtr p;
		
		p = Database.PackStart ();

		Database.PackString (p, title);

		Database.PackInt (p, artists.Length);
		foreach (string artist in artists)
			Database.PackString (p, artist);

		Database.PackInt (p, performers.Length);
		foreach (string performer in performers)
			Database.PackString (p, performer);
		
		Database.PackString (p, album);
		Database.PackInt (p, track_number);
		Database.PackInt (p, disc_number);
		Database.PackString (p, year);
		Database.PackInt (p, duration);
		Database.PackInt (p, mtime);
		Database.PackBool (p, checked_cover_image);
		Database.PackDouble (p, gain);
		Database.PackDouble (p, peak);

		return Database.PackEnd (p, out length);
	}

	public static Song FromHandle (IntPtr handle)
	{
		return (Song) pointers [handle];
	}

	public bool FitsCriteria (string [] search_bits)
	{
		int n_matches = 0;
			
		foreach (string search_bit in search_bits) {
			if (SearchKey.IndexOf (search_bit) >= 0) {
				n_matches++;
				continue;
			}
		}

		return (n_matches == search_bits.Length);
	}
}
