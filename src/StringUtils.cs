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
 */

using System;
using System.Runtime.InteropServices;

public class StringUtils
{
	[DllImport ("libc")]
	private static extern int strlen (string str);

	public static uint GetByteLength (string str)
	{
		return (uint) strlen (str);
	}
	
	public static string SecondsToString (long time)
	{
		int h, m, s;

		h = (int) (time / 3600);
		m = (int) ((time % 3600) / 60);
		s = (int) ((time % 3600) % 60);

		if (h > 0) {
			return h + ":" + m.ToString ("d2") + ":" + s.ToString ("d2");
		} else {
			return m + ":" + s.ToString ("d2");
		}
	}

	public static string JoinHumanReadable (string [] strings, int max)
	{
		string ret;

		if (strings.Length == 0)
			ret = "Unknown";
		else if (strings.Length == 1) 
			ret = strings [0];
		else if (max > 1 && strings.Length > max)
			ret = String.Join (", ", strings, 0, max) + " and others";
		else
			ret = String.Join (", ", strings, 0, strings.Length - 1) + " and " + strings [strings.Length - 1];

		return ret;
	}

	public static string JoinHumanReadable (string [] strings)
	{
		return JoinHumanReadable (strings, -1);
	}
}
