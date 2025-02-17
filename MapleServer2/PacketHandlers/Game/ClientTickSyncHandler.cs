﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game
{
    public class ClientTickSyncHandler : GamePacketHandler
    {
        public override RecvOp OpCode => RecvOp.RESPONSE_CLIENTTICK_SYNC;

        public ClientTickSyncHandler() : base() { }

        public override void Handle(GameSession session, PacketReader packet)
        {
            session.ClientTick = packet.ReadInt();
            session.ServerTick = packet.ReadInt();
        }
    }
}
