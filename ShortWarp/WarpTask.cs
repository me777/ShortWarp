using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReforgedEdenMKII
{
    internal class WarpTask
    {
        internal static readonly ushort WARPTASK_PLAYER_CHANGEPF = 61001;
        internal static readonly ushort WARPTASK_ENTITY_CHANGEPF = 61002;

        internal long Timestamp { get; set; }
        internal int EntityId { get; set; }
        internal bool IsPlayer { get; set; }
        internal string Playfield { get; set; }
        internal PVector3 Position { get; set; }
        internal PVector3 Rotation { get; set; }

        internal void Warp(ModGameAPI api)
        {


            api.Game_Request(
                CmdId.Request_Player_ChangePlayerfield,
                WARPTASK_PLAYER_CHANGEPF,
                new IdPlayfieldPositionRotation(EntityId, Playfield, Position, Rotation));
        }
    }
}
