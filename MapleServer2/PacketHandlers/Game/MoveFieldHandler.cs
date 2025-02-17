﻿using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game
{
    public class MoveFieldHandler : GamePacketHandler
    {
        public override RecvOp OpCode => RecvOp.REQUEST_MOVE_FIELD;

        public MoveFieldHandler() : base() { }

        private enum RequestMoveFieldMode : byte
        {
            Move = 0x0,
            LeaveInstance = 0x1,
            VisitHouse = 0x02,
            ReturnMap = 0x03,
            EnterDecorPlaner = 0x04
        }

        private enum PortalTypes
        {
            Field = 0,
            DungeonReturnToLobby = 1,
            DungeonEnter = 9,
            LeaveDungeon = 13
        }

        public override void Handle(GameSession session, PacketReader packet)
        {
            RequestMoveFieldMode mode = (RequestMoveFieldMode) packet.ReadByte();

            switch (mode)
            {
                case RequestMoveFieldMode.Move:
                    HandleMove(session, packet);
                    break;
                case RequestMoveFieldMode.LeaveInstance:
                    HandleLeaveInstance(session);
                    break;
                case RequestMoveFieldMode.VisitHouse:
                    HandleVisitHouse(session, packet);
                    break;
                case RequestMoveFieldMode.ReturnMap:
                    HandleReturnMap(session);
                    break;
                case RequestMoveFieldMode.EnterDecorPlaner:
                    HandleEnterDecorPlaner(session);
                    break;
                default:
                    IPacketHandler<GameSession>.LogUnknownMode(mode);
                    break;
            }
        }

        private static void HandleMove(GameSession session, PacketReader packet)
        {
            int srcMapId = packet.ReadInt();
            if (srcMapId != session.FieldManager.MapId)
            {
                return;
            }

            int portalId = packet.ReadInt();
            MapPortal srcPortal = MapEntityStorage.GetPortals(srcMapId)
                .FirstOrDefault(portal => portal.Id == portalId);
            if (srcPortal == default)
            {
                Logger.Warn($"Unable to find portal:{portalId} in map:{srcMapId}");
                return;
            }
            switch ((PortalTypes) srcPortal.PortalType)
            {
                case PortalTypes.Field:
                    break;
                case PortalTypes.DungeonReturnToLobby:
                    DungeonSession dungeonSession = GameServer.DungeonManager.GetDungeonSessionByInstanceId(session.Player.InstanceId);
                    if (dungeonSession == null)
                    {
                        return;
                    }
                    session.Player.Warp(dungeonSession.DungeonLobbyId, instanceId: dungeonSession.DungeonInstanceId);
                    return;
                case PortalTypes.LeaveDungeon:
                    HandleLeaveInstance(session);
                    return;
                default:
                    Logger.Warn($"unknown portal type id: {srcPortal.PortalType}");
                    break;
            }

            if (!MapEntityStorage.HasSafePortal(srcMapId) || srcPortal.Target == 0) // map is instance only
            {
                HandleLeaveInstance(session);
                return;
            }

            MapPortal dstPortal = MapEntityStorage.GetPortals(srcPortal.Target)
            .FirstOrDefault(portal => portal.Id == srcPortal.TargetPortalId); // target map's portal id == source portal's targetPortalId
            if (dstPortal == default)
            {
                session.Player.Warp(srcPortal.Target);
                return;
            }

            CoordF coord = dstPortal.Coord.ToFloat();

            if (dstPortal.Name == "Portal_cube") // spawn on the next block if portal is a cube
            {
                if (dstPortal.Rotation.Z == Direction.SOUTH_EAST)
                {
                    coord.Y -= Block.BLOCK_SIZE;
                }
                else if (dstPortal.Rotation.Z == Direction.NORTH_EAST)
                {
                    coord.X += Block.BLOCK_SIZE;
                }
                else if (dstPortal.Rotation.Z == Direction.NORTH_WEST)
                {
                    coord.Y += Block.BLOCK_SIZE;
                }
                else if (dstPortal.Rotation.Z == Direction.SOUTH_WEST)
                {
                    coord.X -= Block.BLOCK_SIZE;
                }
            }

            session.Player.Warp(srcPortal.Target, coord, dstPortal.Rotation.ToFloat());
        }

        private static void HandleLeaveInstance(GameSession session)
        {
            Player player = session.Player;
            player.Warp(player.ReturnMapId, player.ReturnCoord, player.Rotation, instanceId: 0);
        }

        private static void HandleVisitHouse(GameSession session, PacketReader packet)
        {
            int returnMapId = packet.ReadInt();
            packet.Skip(8);
            long accountId = packet.ReadLong();
            string password = packet.ReadUnicodeString();

            Player target = GameServer.Storage.GetPlayerByAccountId(accountId);
            Player player = session.Player;

            Home home = target.Account.Home;
            if (target == null || home == null)
            {
                session.SendNotice("This player does not have a home!");
                return;
            }

            if (player.VisitingHomeId == home.Id && player.MapId == (int) Map.PrivateResidence)
            {
                session.SendNotice($"You are already at {target.Name}'s home!");
                return;
            }

            if (home.IsPrivate)
            {
                if (password == "")
                {
                    session.Send(EnterUGCMapPacket.RequestPassword(accountId));
                    return;
                }

                if (home.Password != password)
                {
                    session.Send(EnterUGCMapPacket.WrongPassword(accountId));
                    return;
                }
            }

            player.VisitingHomeId = home.Id;
            session.Send(ResponseCubePacket.LoadHome(target.Session.FieldPlayer));

            player.Warp(home.MapId, player.Coord, player.Rotation, instanceId: home.InstanceId);
        }

        // This also leaves decor planning
        private static void HandleReturnMap(GameSession session)
        {
            Player player = session.Player;
            if (player.IsInDecorPlanner)
            {
                player.IsInDecorPlanner = false;
                player.Guide = null;
                player.Warp((int) Map.PrivateResidence, instanceId: --player.InstanceId);
                return;
            }

            CoordF returnCoord = player.ReturnCoord;
            returnCoord.Z += Block.BLOCK_SIZE;
            player.Warp(player.ReturnMapId, returnCoord, player.Rotation);
            player.ReturnMapId = 0;
            player.VisitingHomeId = 0;
        }

        private static void HandleEnterDecorPlaner(GameSession session)
        {
            Player player = session.Player;
            if (player.IsInDecorPlanner)
            {
                return;
            }

            player.IsInDecorPlanner = true;
            player.Guide = null;
            Home home = GameServer.HomeManager.GetHome(player.VisitingHomeId);
            home.DecorPlannerHeight = home.Height;
            home.DecorPlannerSize = home.Size;
            home.DecorPlannerInventory = new Dictionary<long, Cube>();
            player.Warp((int) Map.PrivateResidence, instanceId: ++player.InstanceId);
        }
    }
}
