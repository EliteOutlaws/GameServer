﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Packets.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.Other;
using LeagueSandbox.GameServer.GameObjects.Spells;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net;

namespace LeagueSandbox.GameServer.API
{
    public static class ApiFunctionManager
    {
        private static Game _game;
        private static ILog _logger;

        public static byte[] StringToByteArray(string hex)
        {
            hex = hex.Replace(" ", string.Empty);
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        internal static void SetGame(Game game)
        {
            _game = game;
            _logger = LoggerProvider.GetLogger();
        }

        public static void LogInfo(string format)
        {
            _logger.Info(format);
        }

        public static void LogInfo(string format, params object[] args)
        {
            _logger.Info(string.Format(format, args));
        }

        public static void LogDebug(string format)
        {
            _logger.Debug(format);
        }

        public static void LogDebug(string format, params object[] args)
        {
            _logger.Debug(string.Format(format, args));
        }

        public static GameScriptTimer CreateTimer(float duration, Action callback)
        {
            var newTimer = new GameScriptTimer(duration, callback);
            _game.AddGameScriptTimer(newTimer);

            return newTimer;
        }

        public static Buff AddBuffHudVisual(string buffName, float duration, int stacks, BuffType buffType, ObjAiBase onto, float removeAfter = -1.0f)
        {
            return AddBuffHudVisual(buffName, duration, stacks, buffType, onto, onto, removeAfter);
        }

        public static Buff AddBuffHudVisual(string buffName, float duration, int stacks, BuffType buffType, ObjAiBase onto, ObjAiBase from, float removeAfter = -1.0f)
        {
            var b = new Buff(_game, buffName, duration, stacks, buffType, onto, from);
            _game.PacketNotifier.NotifyAddBuff(b);
            if (removeAfter >= 0)
            {
                CreateTimer(removeAfter, () => RemoveBuffHudVisual(b));
            }

            return b;
        }

        public static void RemoveBuffHudVisual(Buff b)
        {
            _game.PacketNotifier.NotifyRemoveBuff(b.TargetUnit, b.Name, b.Slot);
            b.TargetUnit.RemoveBuffSlot(b);
        }

        public static void SetGameObjectVisibility(GameObject gameObject, bool visibility)
        {
            var teams = GetTeams();
            foreach (var id in teams)
            {
                gameObject.SetVisibleByTeam(id, visibility);
            }
        }

        public static List<TeamId> GetTeams()
        {
            return _game.ObjectManager.Teams;
        }

        public static void TeleportTo(ObjAiBase unit, float x, float y)
        {
            var coords = new Vector2(x, y);
            var truePos = _game.Map.NavGrid.GetClosestTerrainExit(coords);

            CancelDash(unit);
            unit.TeleportTo(truePos.X, truePos.Y);
        }

        public static bool IsWalkable(float x, float y)
        {
            return _game.Map.NavGrid.IsWalkable(x, y);
        }

        public static void AddBuff(string buffName, float duration, int stacks, BuffType buffType, ObjAiBase onto, ObjAiBase from)
        {
            var buff = new Buff(_game, buffName, duration, stacks, buffType, onto, from);
            onto.AddBuff(buff);
            _game.PacketNotifier.NotifyAddBuff(buff);
        }

        public static void EditBuff(Buff b, int newStacks)
        {
            b.SetStacks(newStacks);
            _game.PacketNotifier.NotifyEditBuff(b, newStacks);
        }

        public static Particle AddParticle(Champion champion, string particle, float toX, float toY, float size = 1.0f, string bone = "")
        {
            var t = new Target(toX, toY);
            var p = new Particle(_game, champion, t, particle, size, bone);
            _game.PacketNotifier.NotifyParticleSpawn(p);
            return p;
        }

        public static Particle AddParticleTarget(Champion champion, string particle, Target target, float size = 1.0f, string bone = "")
        {
            var p = new Particle(_game, champion, target, particle, size, bone);
            _game.PacketNotifier.NotifyParticleSpawn(p);
            return p;
        }

        public static void RemoveParticle(Particle p)
        {
            _game.PacketNotifier.NotifyParticleDestroy(p);
        }

        public static void PrintChat(string msg)
        {
            _game.PacketNotifier.NotifyDebugMessage(msg);
        }

        public static void FaceDirection(AttackableUnit unit, Vector2 direction, bool instant = true, float turnTime = 0.0833f)
        {
            _game.PacketNotifier.NotifyFaceDirection(unit, direction, instant, turnTime);
            // todo change units direction
        }

        public static List<AttackableUnit> GetUnitsInRange(Target target, float range, bool isAlive)
        {
            return _game.ObjectManager.GetUnitsInRange(target, range, isAlive);
        }

        public static List<Champion> GetChampionsInRange(Target target, float range, bool isAlive)
        {
            return _game.ObjectManager.GetChampionsInRange(target, range, isAlive);
        }

        public static void CancelDash(ObjAiBase unit)
        {
            // Allow the user to move the champion
            unit.SetDashingState(false);

            // Reset the default run animation
            var animList = new List<string> { "RUN", "" };
            _game.PacketNotifier.NotifySetAnimation(unit, animList);
        }

        public static void DashToUnit(ObjAiBase unit,
                                  Target target,
                                  float dashSpeed,
                                  bool keepFacingLastDirection,
                                  string animation = null,
                                  float leapHeight = 0.0f,
                                  float followTargetMaxDistance = 0.0f,
                                  float backDistance = 0.0f,
                                  float travelTime = 0.0f
                                  )
        {
            if (animation != null)
            {
                var animList = new List<string> { "RUN", animation };
                _game.PacketNotifier.NotifySetAnimation(unit, animList);
            }

            if (target.IsSimpleTarget)
            {
                var newCoords = _game.Map.NavGrid.GetClosestTerrainExit(new Vector2(target.X, target.Y));
                var newTarget = new Target(newCoords);
                unit.DashToTarget(newTarget, dashSpeed, followTargetMaxDistance, backDistance, travelTime);
                _game.PacketNotifier.NotifyDash(
                    unit,
                    newTarget,
                    dashSpeed,
                    keepFacingLastDirection,
                    leapHeight,
                    followTargetMaxDistance,
                    backDistance,
                    travelTime
                );
            }
            else
            {
                unit.DashToTarget(target, dashSpeed, followTargetMaxDistance, backDistance, travelTime);
                _game.PacketNotifier.NotifyDash(
                    unit,
                    target,
                    dashSpeed,
                    keepFacingLastDirection,
                    leapHeight,
                    followTargetMaxDistance,
                    backDistance,
                    travelTime
                );
            }
            unit.TargetUnit = null;
        }

        public static void DashToLocation(ObjAiBase unit,
                                 float x,
                                 float y,
                                 float dashSpeed,
                                 bool keepFacingLastDirection,
                                 string animation = null,
                                 float leapHeight = 0.0f,
                                 float followTargetMaxDistance = 0.0f,
                                 float backDistance = 0.0f,
                                 float travelTime = 0.0f
                                 )
        {
            DashToUnit(
                unit,
                new Target(x, y),
                dashSpeed,
                keepFacingLastDirection,
                animation,
                leapHeight,
                followTargetMaxDistance,
                backDistance,
                travelTime
            );
        }

        public static void SendPacket(string packetString)
        {
            var packet = StringToByteArray(packetString);
            _game.PacketHandlerManager.BroadcastPacket(packet, Channel.CHL_S2C);
        }
    }
}
