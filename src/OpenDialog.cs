/*
 * Copyright (C) 2005 Tamara Roberson <foxxygirltamara@gmail.com>
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
using Gtk;
using Mono.Posix;

namespace Muine
{
	public class OpenDialog : FileSelector
	{	
		// Constants
		// Constants :: GConf
		private const string GConfKeyDefaultPlaylistFolder = "/apps/muine/default_playlist_folder";
		
		// Strings
		private static readonly string string_title =
			Catalog.GetString ("Open Playlist");
		private static readonly string string_filter =
			Catalog.GetString ("Playlist files");

		// Constructor
		public OpenDialog () 
		: base (string_title, Global.Playlist, FileChooserAction.Open, GConfKeyDefaultPlaylistFolder)
		{
			FileFilter filter = new FileFilter ();
			filter.Name = string_filter;
			filter.AddMimeType ("audio/x-mpegurl");
			filter.AddPattern ("*.m3u");
			base.AddFilter (filter);

			string fn = base.GetFile ();

			if (fn.Length == 0 || !FileUtils.IsPlaylist (fn))
				return;

			if (FileUtils.Exists (fn))
				Global.Playlist.OpenPlaylist (fn);
		}
	}
}