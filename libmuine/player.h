/* -*- mode: C; c-file-style: "gnu" -*- */
/*
 * Copyright (C) 2003 Richard Hult <richard@imendio.com>
 *           (C) 2004 Jorn Baayen <jorn@nl.linux.org>
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

#ifndef __PLAYER_H__
#define __PLAYER_H__

#include <glib-object.h>
#include <gst/gst.h>

#define TYPE_PLAYER            (player_get_type ())
#define PLAYER(obj)            (G_TYPE_CHECK_INSTANCE_CAST ((obj), TYPE_PLAYER, Player))
#define PLAYER_CLASS(klass)    (G_TYPE_CHECK_CLASS_CAST ((klass), TYPE_PLAYER, PlayerClass))
#define IS_PLAYER(obj)         (G_TYPE_CHECK_INSTANCE_TYPE ((obj), TYPE_PLAYER))
#define IS_PLAYER_CLASS(klass) (G_TYPE_CHECK_CLASS_TYPE ((klass), TYPE_PLAYER))
#define PLAYER_GET_CLASS(obj)  (G_TYPE_INSTANCE_GET_CLASS ((obj), TYPE_PLAYER, PlayerClass))

typedef struct _Player      Player;
typedef struct _PlayerClass PlayerClass;
typedef struct _PlayerPriv  PlayerPriv;

typedef enum {
  PLAYER_STATE_STOPPED,
  PLAYER_STATE_PLAYING,
  PLAYER_STATE_PAUSED
} PlayerState;

struct _Player
{
  GObject parent;
  PlayerPriv *priv;
};

struct _PlayerClass
{
  GObjectClass parent_class;
};

GType        player_get_type       (void);
Player *     player_new            (void);
gboolean     player_set_file       (Player     *player,
				    const char *filename,
				    const char *mime_type);
const char * player_get_file       (Player     *player);
gboolean     player_play           (Player     *player);
void         player_stop           (Player     *player);
void         player_pause          (Player     *player);
void         player_set_volume     (Player     *player,
				    int         volume);
int          player_get_volume     (Player     *player);
void         player_set_replaygain (Player     *player,
				    double      gain,
				    double      peak);
void         player_toggle_mute    (Player     *player);
PlayerState  player_get_state      (Player     *player);
void         player_seek           (Player     *player,
				    int         t);
int          player_tell           (Player     *player);
gboolean     player_is_playing     (Player     *player,
				    const char *filename);

#endif /* __PLAYER_H__ */
