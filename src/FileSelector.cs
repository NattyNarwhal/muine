/*
 * Copyright (C) 2004 Jorn Baayen <jorn@nl.linux.org>
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
using System.IO;

using Gtk;
using GLib;

public class FileSelector : FileChooserDialog
{
	private string gconf_path;

	public FileSelector (string title, Window parent, FileChooserAction action, string gcp) : base (title, null, action)
	{
		TransientFor = parent;
		LocalOnly = true;
		AddButton (Stock.Cancel, ResponseType.Cancel);
		if (action == FileChooserAction.Open)
	                AddButton (Stock.Open, ResponseType.Ok);
		else if (action == FileChooserAction.Save)
	                AddButton (Stock.Save, ResponseType.Ok);
                DefaultResponse = ResponseType.Ok;

		gconf_path = gcp;

		string start_dir;
		try {
			start_dir = (string) Muine.GConfClient.Get (gconf_path);
		} catch {
			start_dir = "~";
		}

		start_dir.Replace ("~", Environment.GetEnvironmentVariable ("HOME"));

		if (start_dir.EndsWith ("/") == false)
			start_dir += "/";

		SetCurrentFolderUri (start_dir);
	}

	public string GetFile ()
	{
		if (Run () != (int) ResponseType.Ok) {
			Destroy ();

			return "";
		}

		string ret = StringUtils.LocalPathFromUri (Uri);
		
		Muine.GConfClient.Set (gconf_path,
		                       System.IO.Path.GetDirectoryName (ret) + "/");

		Destroy ();

		return ret;
	}
}
