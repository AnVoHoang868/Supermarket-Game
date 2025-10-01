#if UNITY_SERVER
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Server.Database
{
    public class RoomRepository
    {
        private SQLiteConnection _db;

        public RoomRepository(SQLiteConnection db)
        {
            _db = db;
        }

        public Room Create(Room room)
        {
            room.CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
            _db.Insert(room);
            return room;
        }

        public Room GetById(string id)
        {
            return _db.Table<Room>().FirstOrDefault(r => r.Id == id);
        }

        public void UpdatePlayerCount(string roomId, int count)
        {
            var room = GetById(roomId);
            if (room != null)
            {
                room.PlayerCount = count;
                _db.Update(room);
            }
        }

        public void UpdateStatus(string roomId, RoomStatus status)
        {
            var room = GetById(roomId);
            if (room != null)
            {
                room.Status = (int)status;
                _db.Update(room);
            }
        }

        public List<Room> GetWaitingRooms()
        {
            return _db.Table<Room>()
                .Where(r => r.Status == (int)RoomStatus.Waiting && r.PlayerCount < 4)
                .ToList();
        }

        // RoomPlayer operations
        public void AddPlayerToRoom(RoomPlayer roomPlayer)
        {
            roomPlayer.JoinedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
            _db.Insert(roomPlayer);
        }

        public List<RoomPlayer> GetRoomPlayers(string roomId)
        {
            return _db.Table<RoomPlayer>()
                .Where(rp => rp.RoomId == roomId)
                .ToList();
        }

        public void RemovePlayerFromRoom(string roomId, string userId)
        {
            _db.Execute("DELETE FROM RoomPlayers WHERE RoomId = ? AND UserId = ?", roomId, userId);
        }

        public void UpdatePlayerReady(string roomId, string userId, bool isReady)
        {
            var player = _db.Table<RoomPlayer>()
                .FirstOrDefault(rp => rp.RoomId == roomId && rp.UserId == userId);
            
            if (player != null)
            {
                player.IsReady = isReady ? 1 : 0;
                _db.Update(player);
            }
        }
    }
}
#endif