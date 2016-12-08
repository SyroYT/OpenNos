﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EpPathFinding;
using OpenNos.Core;
using OpenNos.Data;
using OpenNos.DAL;
using OpenNos.Domain;

namespace OpenNos.GameObject
{
    public class Map : BroadcastableBase, IMapDTO
    {
        #region Members

        private readonly ThreadSafeSortedList<long, MapMonster> _monsters;
        private bool _isSleeping;
        private bool _isSleepingRequest;
        private bool _disposed;
        private List<int> _mapMonsterIds;
        private List<MapNpc> _npcs;
        private List<PortalDTO> _portals;
        private Random _random;
        private Guid _uniqueIdentifier;

        #endregion

        #region Instantiation

        public Map(short mapId, Guid uniqueIdentifier, byte[] data)
        {
            _isSleeping = true;
            LastUserShopId = 0;
            _random = new Random();
            MapId = mapId;
            ShopAllowed = true;
            _uniqueIdentifier = uniqueIdentifier;
            _monsters = new ThreadSafeSortedList<long, MapMonster>();
            _mapMonsterIds = new List<int>();
            Data = data;
            LoadZone();
            IEnumerable<PortalDTO> portals = DAOFactory.PortalDAO.LoadByMap(MapId).ToList();
            DroppedList = new ThreadSafeSortedList<long, MapItem>();
            MapTypes = new List<MapTypeDTO>();
            foreach (MapTypeMapDTO maptypemap in DAOFactory.MapTypeMapDAO.LoadByMapId(mapId).ToList())
            {
                MapTypeDTO maptype = DAOFactory.MapTypeDAO.LoadById(maptypemap.MapTypeId);
                MapTypes.Add(maptype);
            }

            if (MapTypes.Any())
            {
                if (MapTypes.ElementAt(0).RespawnMapTypeId != null)
                {
                    long? respawnMapTypeId = MapTypes.ElementAt(0).RespawnMapTypeId;
                    if (respawnMapTypeId != null)
                        DefaultRespawn = DAOFactory.RespawnMapTypeDAO.LoadById((long)respawnMapTypeId);
                    long? returnMapTypeId = MapTypes.ElementAt(0).ReturnMapTypeId;
                    if (returnMapTypeId != null)
                        DefaultReturn = DAOFactory.RespawnMapTypeDAO.LoadById((long)returnMapTypeId);
                }
            }
            _portals = new List<PortalDTO>();
            foreach (PortalDTO portal in portals)
            {
                _portals.Add(portal);
            }

            UserShops = new Dictionary<long, MapShop>();
            _npcs = new List<MapNpc>();
            _npcs.AddRange(ServerManager.Instance.GetMapNpcsByMapId(MapId).AsEnumerable());
        }

        #endregion

        #region Properties

        public byte[] Data { get; set; }

        public RespawnMapTypeDTO DefaultRespawn
        {
            get; set;
        }

        public RespawnMapTypeDTO DefaultReturn
        {
            get; set;
        }

        public ThreadSafeSortedList<long, MapItem> DroppedList { get; set; }

        public StaticGrid Grid { get; set; }

        public bool IsDancing { get; set; }

        public bool IsSleeping
        {
            get
            {
                if (_isSleepingRequest && !_isSleeping && LastUnregister.AddSeconds(30) < DateTime.Now)
                {
                    Grid = null;
                    _isSleeping = true;
                    _isSleepingRequest = false;
                    return true;
                }
                if (_isSleeping)
                {
                    return true;
                }

                if (Grid == null)
                {
                    LoadZone();
                }
                return false;
            }
            set
            {
                if(value == true)
                {
                    _isSleepingRequest = true;
                }
                else
                {
                    _isSleeping = false;
                }
            }
        }

        public long LastUserShopId { get; set; }

        public short MapId { get; set; }

        public List<MapTypeDTO> MapTypes
        {
            get; set;
        }

        /// <summary>
        /// This list ONLY for READ access to MapMonster, you CANNOT MODIFY them here. Use
        /// Add/RemoveMonster instead.
        /// </summary>
        public List<MapMonster> Monsters
        {
            get
            {
                return _monsters.GetAllItems();
            }
        }

        public int Music { get; set; }

        public string Name { get; set; }

        public List<MapNpc> Npcs
        {
            get
            {
                return _npcs;
            }
        }

        public List<PortalDTO> Portals
        {
            get
            {
                return _portals;
            }
        }

        public bool ShopAllowed { get; set; }

        public Dictionary<long, MapShop> UserShops { get; set; }

        public int XLength { get; set; }

        public int YLength { get; set; }

        #endregion

        #region Methods

        public static int GetDistance(Character character1, Character character2)
        {
            return GetDistance(new MapCell { MapId = character1.MapId, X = character1.MapX, Y = character1.MapY }, new MapCell { MapId = character2.MapId, X = character2.MapX, Y = character2.MapY });
        }

        public static int GetDistance(MapCell p, MapCell q)
        {
            return Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y));
        }

        public void AddMonster(MapMonster monster)
        {
            _monsters[monster.MapMonsterId] = monster;
        }

        public override void Dispose()
        {
            if (!_disposed)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
                _disposed = true;
            }
        }

        public void DropItemByMonster(long? owner, DropDTO drop, short mapX, short mapY, int gold = 0)
        {
            try
            {
                short localMapX = (short)_random.Next(mapX - 1, mapX + 1);
                short localMapY = (short)_random.Next(mapY - 1, mapY + 1);
                List<MapCell> possibilities = new List<MapCell>();

                for (short x = -1; x < 2; x++)
                {
                    for (short y = -1; y < 2; y++)
                    {
                        possibilities.Add(new MapCell { X = x, Y = y });
                    }
                }

                foreach (MapCell possibilitie in possibilities.OrderBy(s => _random.Next()))
                {
                    localMapX = (short)(mapX + possibilitie.X);
                    localMapY = (short)(mapY + possibilitie.Y);
                    if (!IsBlockedZone(localMapX, localMapY))
                    {
                        break;
                    }
                }

                MonsterMapItem droppedItem = new MonsterMapItem(localMapX, localMapY, drop.ItemVNum, drop.Amount, owner ?? -1);

                DroppedList[droppedItem.TransportId] = droppedItem;

                Broadcast($"drop {droppedItem.ItemVNum} {droppedItem.TransportId} {droppedItem.PositionX} {droppedItem.PositionY} {(droppedItem.GoldAmount > 1 ? droppedItem.GoldAmount : droppedItem.Amount)} 0 0 -1");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public IEnumerable<string> GenerateUserShops()
        {
            return UserShops.Select(shop => $"shop 1 {shop.Value.OwnerId} 1 3 0 {shop.Value.Name}").ToList();
        }

        public List<MapMonster> GetListMonsterInRange(short mapX, short mapY, byte distance)
        {
            return _monsters.GetAllItems().Where(s => s.IsAlive && s.IsInRange(mapX, mapY, distance)).ToList();
        }

        public MapMonster GetMonster(long mapMonsterId)
        {
            return _monsters[mapMonsterId];
        }

        public int GetNextMonsterId()
        {
            int nextId = _mapMonsterIds.Any() ? _mapMonsterIds.Last() + 1 : 1;
            _mapMonsterIds.Add(nextId);
            return nextId;
        }

        public bool IsBlockedZone(int x, int y)
        {
            if (Grid != null)
            {
                if (!Grid.IsWalkableAt(new GridPos(x, y)))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsBlockedZone(int firstX, int firstY, int mapX, int mapY)
        {
            for (int i = 1; i <= Math.Abs(mapX - firstX); i++)
            {
                if (IsBlockedZone(firstX + Math.Sign(mapX - firstX) * i, firstY))
                {
                    return true;
                }
            }

            for (int i = 1; i <= Math.Abs(mapY - firstY); i++)
            {
                if (IsBlockedZone(firstX, firstY + Math.Sign(mapY - firstY) * i))
                {
                    return true;
                }
            }
            return false;
        }

        public List<GridPos> JpsPlus(JumpPointParam jumpPointParameters, GridPos cell1, GridPos cell2)
        {
            List<GridPos> lpath = new List<GridPos>();
            if (jumpPointParameters != null)
            {
                jumpPointParameters.Reset(cell1, cell2);
                List<GridPos> resultPathList = JumpPointFinder.FindPath(jumpPointParameters);
                lpath = JumpPointFinder.GetFullPath(resultPathList);
            }
            return lpath;
        }

        public void LoadMonsters()
        {
            foreach (MapMonsterDTO monster in DAOFactory.MapMonsterDAO.LoadFromMap(MapId).ToList())
            {
                _monsters[monster.MapMonsterId] = monster as MapMonster;
                _mapMonsterIds.Add(monster.MapMonsterId);
            }
        }

        public void LoadZone()
        {
            using (Stream stream = new MemoryStream(Data))
            {
                int numBytesToRead = 1;
                int numBytesRead = 0;
                byte[] bytes = new byte[numBytesToRead];

                byte[] xlength = new byte[2];
                byte[] ylength = new byte[2];
                stream.Read(bytes, numBytesRead, numBytesToRead);
                xlength[0] = bytes[0];
                stream.Read(bytes, numBytesRead, numBytesToRead);
                xlength[1] = bytes[0];
                stream.Read(bytes, numBytesRead, numBytesToRead);
                ylength[0] = bytes[0];
                stream.Read(bytes, numBytesRead, numBytesToRead);
                ylength[1] = bytes[0];
                YLength = BitConverter.ToInt16(ylength, 0);
                XLength = BitConverter.ToInt16(xlength, 0);

                Grid = new StaticGrid(XLength, YLength);
                for (int i = 0; i < YLength; ++i)
                {
                    for (int t = 0; t < XLength; ++t)
                    {
                        stream.Read(bytes, numBytesRead, numBytesToRead);
                        Grid.SetWalkableAt(new GridPos(t, i), bytes[0]);
                    }
                }
            }
        }

        public MapItem PutItem(InventoryType type, short slot, byte amount, ref ItemInstance inv, ClientSession session)
        {
            Logger.Debug($"type: {type} slot: {slot} amount: {amount}", session.SessionId);
            Guid random2 = Guid.NewGuid();
            MapItem droppedItem = null;
            List<GridPos> possibilities = new List<GridPos>();

            for (short x = -2; x < 3; x++)
            {
                for (short y = -2; y < 3; y++)
                {
                    possibilities.Add(new GridPos { X = x, Y = y });
                }
            }

            short mapX = 0;
            short mapY = 0;
            bool niceSpot = false;
            foreach (GridPos possibilitie in possibilities.OrderBy(s => _random.Next()))
            {
                mapX = (short)(session.Character.MapX + possibilitie.X);
                mapY = (short)(session.Character.MapY + possibilitie.Y);
                if (!IsBlockedZone(mapX, mapY))
                {
                    niceSpot = true;
                    break;
                }
            }

            if (niceSpot)
            {
                if (amount > 0 && amount <= inv.Amount)
                {
                    ItemInstance newItemInstance = inv.DeepCopy();
                    newItemInstance.Id = random2;
                    newItemInstance.Amount = amount;
                    droppedItem = new CharacterMapItem(mapX, mapY, newItemInstance);

                    DroppedList[droppedItem.TransportId] = droppedItem;
                    inv.Amount -= amount;
                }
            }
            return droppedItem;
        }

        public void RemoveMapItem()
        {
            // take the data from list to remove it without having enumeration problems (ToList)
            try
            {
                List<MapItem> dropsToRemove = DroppedList.GetAllItems().Where(dl => dl.CreatedDate.AddMinutes(3) < DateTime.Now).ToList();

                foreach (MapItem drop in dropsToRemove)
                {
                    Broadcast(drop.GenerateOut(drop.TransportId));
                    DroppedList.Remove(drop.TransportId);
                    TransportFactory.Instance.RemoveTransportId(drop.TransportId);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public void RemoveMonster(MapMonster monsterToRemove)
        {
            _monsters.Remove(monsterToRemove.MapMonsterId);
        }

        public void SetMapMapMonsterReference()
        {
            foreach (MapMonster monster in _monsters.GetAllItems())
            {
                monster.Map = this;
            }
        }

        public void SetMapMapNpcReference()
        {
            foreach (MapNpc npc in _npcs)
            {
                npc.Map = this;
                npc.JumpPointParameters = new JumpPointParam(Grid, new GridPos(0, 0), new GridPos(0, 0), false, true, true, HeuristicMode.Manhattan);
            }
        }

        internal IEnumerable<Character> GetCharactersInRange(short mapX, short mapY, byte distance)
        {
            List<Character> characters = new List<Character>();
            IEnumerable<ClientSession> cl = Sessions.Where(s => s.HasSelectedCharacter && s.Character.Hp > 0);
            IEnumerable<ClientSession> clientSessions = cl as ClientSession[] ?? cl.ToArray();
            for (int i = clientSessions.Count() - 1; i >= 0; i--)
            {
                if (GetDistance(new MapCell { X = mapX, Y = mapY }, new MapCell { X = clientSessions.ElementAt(i).Character.MapX, Y = clientSessions.ElementAt(i).Character.MapY }) <= distance + 1)
                {
                    characters.Add(clientSessions.ElementAt(i).Character);
                }
            }
            return characters;
        }

        internal bool GetFreePosition(ref short firstX, ref short firstY, byte xpoint, byte ypoint)
        {
            short minX = (short)(-xpoint + firstX);
            short maxX = (short)(xpoint + firstX);

            short minY = (short)(-ypoint + firstY);
            short maxY = (short)(ypoint + firstY);

            List<MapCell> cells = new List<MapCell>();
            for (short y = minY; y <= maxY; y++)
            {
                for (short x = minX; x <= maxX; x++)
                {
                    if (x != firstX || y != firstY)
                    {
                        cells.Add(new MapCell { X = x, Y = y, MapId = MapId });
                    }
                }
            }

            foreach (MapCell cell in cells.OrderBy(s => _random.Next(int.MaxValue)))
            {
                if (!IsBlockedZone(firstX, firstY, cell.X, cell.Y))
                {
                    firstX = cell.X;
                    firstY = cell.Y;
                    return true;
                }
            }

            return false;
        }

        internal void RemoveMonstersTarget(long characterId)
        {
            foreach (MapMonster monster in Monsters.Where(m => m.Target == characterId))
            {
                monster.RemoveTarget();
            }
        }

        internal List<GridPos> StraightPath(GridPos mapCell1, GridPos mapCell2)
        {
            List<GridPos> path = new List<GridPos> { mapCell1 };
            do
            {
                if (path.Last().X < mapCell2.X && path.Last().Y < mapCell2.Y)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X + 1), Y = (short)(path.Last().Y + 1) });
                }
                else if (path.Last().X > mapCell2.X && path.Last().Y > mapCell2.Y)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X - 1), Y = (short)(path.Last().Y - 1) });
                }
                else if (path.Last().X < mapCell2.X && path.Last().Y > mapCell2.Y)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X + 1), Y = (short)(path.Last().Y - 1) });
                }
                else if (path.Last().X > mapCell2.X && path.Last().Y < mapCell2.Y)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X - 1), Y = (short)(path.Last().Y + 1) });
                }
                else if (path.Last().X > mapCell2.X)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X - 1), Y = (short)(path.Last().Y) });
                }
                else if (path.Last().X < mapCell2.X)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X + 1), Y = (short)(path.Last().Y) });
                }
                else if (path.Last().Y > mapCell2.Y)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X), Y = (short)(path.Last().Y - 1) });
                }
                else if (path.Last().Y < mapCell2.Y)
                {
                    path.Add(new GridPos { X = (short)(path.Last().X), Y = (short)(path.Last().Y + 1) });
                }
            }
            while ((path.Last().X != mapCell2.X || path.Last().Y != mapCell2.Y) && (!IsBlockedZone(path.Last().X, path.Last().Y)));
            if (IsBlockedZone(path.Last().X, path.Last().Y))
            {
                if (path.Any())
                {
                    path.Remove(path.Last());
                }
            }
            path.RemoveAt(0);
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _monsters.Dispose();
            }
        }

        #endregion
    }
}