﻿using System.Timers;
using ENet;
using GameServerCore.Domain.GameObjects;
using GameServerCore.Packets.Enums;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleUnpauseReq : PacketHandlerBase
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public override PacketCmd PacketType => PacketCmd.PKT_UNPAUSE_GAME;
        public override Channel PacketChannel => Channel.CHL_C2S;

        public HandleUnpauseReq(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(Peer peer, byte[] data)
        {
            if (!_game.IsPaused)
            {
                return false;
            }

            IChampion unpauser = null;
            if (peer != null)
            {
                unpauser = _playerManager.GetPeerInfo(peer).Champion;
            }

            _game.PacketNotifier.NotifyResumeGame(unpauser, true);
            var timer = new Timer
            {
                AutoReset = false,
                Enabled = true,
                Interval = 5000
            };
            timer.Elapsed += (sender, args) =>
            {
                _game.PacketNotifier.NotifyResumeGame(unpauser, false);
                _game.Unpause();
            };
            return true;
        }
    }
}