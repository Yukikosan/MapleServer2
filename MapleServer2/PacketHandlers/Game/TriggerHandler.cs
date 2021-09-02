﻿using Maple2.Trigger.Enum;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;
using Microsoft.Extensions.Logging;
using static MapleServer2.Packets.TriggerPacket;

namespace MapleServer2.PacketHandlers.Game
{
    public class TriggerHandler : GamePacketHandler
    {
        public override RecvOp OpCode => RecvOp.TRIGGER;

        public TriggerHandler(ILogger<TriggerHandler> logger) : base(logger) { }

        private enum TriggerMode : byte
        {
            SkipCutscene = 0x7,
            UpdateWidget = 0x8,
        }

        public override void Handle(GameSession session, PacketReader packet)
        {
            TriggerMode mode = (TriggerMode) packet.ReadByte();

            switch (mode)
            {
                case TriggerMode.SkipCutscene:
                    HandleSkipCutscene(session, packet);
                    break;
                case TriggerMode.UpdateWidget:
                    HandleUpdateWidget(session, packet);
                    break;
                default:
                    IPacketHandler<GameSession>.LogUnknownMode(mode);
                    break;
            }
        }

        private static void HandleSkipCutscene(GameSession session, PacketReader packet)
        {
            session.FieldManager.SkipScene = true;
            // TODO: Start the SkipScene state
        }

        private static void HandleUpdateWidget(GameSession session, PacketReader packet)
        {
            TriggerUIMode submode = (TriggerUIMode) packet.ReadByte();
            int arg = packet.ReadInt();

            Widget widget;
            switch (submode)
            {
                case TriggerUIMode.StopCutscene:
                    widget = session.FieldManager.GetWidget(WidgetType.SceneMovie);
                    if (widget == null)
                    {
                        return;
                    }
                    widget.State = "IsStop";
                    widget.Arg = arg.ToString();
                    session.Send(TriggerPacket.StopCutscene(arg));
                    break;
                case TriggerUIMode.Guide:
                    widget = session.FieldManager.GetWidget(WidgetType.Guide);
                    if (widget == null)
                    {
                        return;
                    }
                    widget.State = "IsTriggerEvent";
                    widget.Arg = arg.ToString();
                    break;
            }
        }
    }
}