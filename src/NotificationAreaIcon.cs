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

// TODO: Make into a plugin?

using System;
using System.Runtime.InteropServices;

using Gtk;
using Gdk;

using Mono.Posix;

using Muine.PluginLib;

namespace Muine
{
	public class NotificationAreaIcon : Plug
	{
		// Strings
		private static readonly string string_program =
			Catalog.GetString ("Muine music player");

		// song artists - song title
		private static readonly string string_tooltip_format = Catalog.GetString ("{0} - {1}");

		// Widgets
		private EventBox ebox;
		private Gtk.Image image;
		private Tooltips tooltips;
		private Menu menu;

		// Objects
		private IPlayer player;

		// Variables
		private int menu_x;
		private int menu_y;
		
		private bool button_down = false;
		private bool visible = false;

		private string tooltip = "";

		private bool playing = false;

		// Constructor
		public NotificationAreaIcon (IPlayer player) : base (IntPtr.Zero)
		{
			// Connect to player
			this.player = player;
			
			player.SongChangedEvent  += new SongChangedEventHandler  (OnSongChangedEvent );
			player.StateChangedEvent += new StateChangedEventHandler (OnStateChangedEvent);
			
			// Build menu
			player.UIManager.AddUiFromResource ("NotificationAreaIcon.xml");
			
			menu = (Menu) player.UIManager.GetWidget ("/Menu");
			menu.Deactivated += new EventHandler (OnMenuDeactivated);

			// Init tooltips -- we init into "not playing" state
			tooltips = new Tooltips ();
			tooltips.Disable ();

			// init icon
			Init ();
		}

		// Destructor
		~NotificationAreaIcon ()
		{
			Dispose ();
		}
		
		// Methods
		// Methods :: Public
		// Methods :: Public :: Init
		[DllImport ("libmuine")]
		private static extern IntPtr egg_tray_icon_new (string name);

		public void Init ()
		{
			Raw = egg_tray_icon_new (string_program);

			DestroyEvent += new DestroyEventHandler (OnDestroyEvent);

			ebox = new EventBox ();
			ebox.ButtonPressEvent += new ButtonPressEventHandler (OnButtonPressEvent);
			
			image = new Gtk.Image ();

			ebox.Add (image);
			Add (ebox);

			UpdateImage ();
			UpdateTooltip ();

			if (visible)
				ShowAll ();
		}

		// Methods :: Public :: Run
		public void Run ()
		{
			visible = true;
			ShowAll ();
		}

		// Methods :: Private
		// Methods :: Private :: UpdateTooltip
		private void UpdateTooltip ()
		{
			tooltips.SetTip (this, tooltip, null);
		}

		// Methods :: Private :: UpdateImage
		private void UpdateImage ()
		{
			string icon = (playing) ? "muine-tray-playing" : "muine-tray-paused";
			image.SetFromStock (icon, IconSize.Menu);
		}

		// Methods :: Private :: Clamp
		private int Clamp (int x, int low, int high)
		{
			return (x > high) 
			       ? high 
			       : 
			       (x < low) 
			       ? low 
			       : 
			       x;
		}

		// Methods :: Private :: PositionMenu
		private void PositionMenu (Menu menu, out int x, out int y, out bool push_in)
		{
			x = menu_x;
			y = menu_y;

			int monitor = ((Widget) menu).Screen.GetMonitorAtPoint (x, y);
			Gdk.Rectangle rect = ((Widget) menu).Screen.GetMonitorGeometry (monitor);

			int space_above = y - rect.Y;
			int space_below = rect.Y + rect.Height - y;

			Requisition requisition = menu.SizeRequest ();

			if (requisition.Height <= space_above ||
			    requisition.Height <= space_below) {
				if (requisition.Height <= space_below)
					y = y + ebox.Allocation.Height;
				else
					y = y - requisition.Height;
			} else if (requisition.Height > space_below && requisition.Height > space_above) {
				if (space_below >= space_above)
					y = rect.Y + rect.Height - requisition.Height;
				else
					y = rect.Y;
			} else {
				y = rect.Y;
			}

			push_in = true;
		}

		// Methods :: Private :: CreateTooltip
		private string CreateTooltip (ISong song)
		{
			return String.Format (string_tooltip_format,
					      StringUtils.JoinHumanReadable (song.Artists),
					      song.Title);
		}

		// Handlers
		// Handlers :: OnSelectionDone
		private void OnSelectionDone (object o, EventArgs args)
		{
			State = StateType.Normal;
		}

		// Handlers :: OnButtonPressEvent
		private void OnButtonPressEvent (object o, ButtonPressEventArgs args)
		{
			switch (args.Event.Button)
			{
			case 1:
			case 3:
				State = StateType.Active;

				menu_x = (int) args.Event.XRoot - (int) args.Event.X;
				menu_y = (int) args.Event.YRoot - (int) args.Event.Y;

				menu.Popup (null, null, new MenuPositionFunc (PositionMenu), IntPtr.Zero,
				            args.Event.Button, args.Event.Time);
				
				break;

			case 2:
				player.WindowVisible = !player.WindowVisible;

				break;

			default:
				break;
			}

			args.RetVal = false;
		}

		// Handlers :: OnMenuDeactivated
		private void OnMenuDeactivated (object o, EventArgs args)
		{
			State = StateType.Normal;
		}

		// Handlers :: OnDestroyEvent
		private void OnDestroyEvent (object o, DestroyEventArgs args)
		{
			Init ();
		}

		// Handlers :: OnSongChangedEvent
		private void OnSongChangedEvent (ISong song)
		{
			tooltip = (song == null)
				  ? null
				  : CreateTooltip (song);

			UpdateTooltip ();
		}

		// Handlers :: OnStateChangedEvent
		private void OnStateChangedEvent (bool playing)
		{
			if (playing)
				tooltips.Enable ();
			else
				tooltips.Disable ();

			this.playing = playing;

			UpdateImage ();
		}
	}
}
