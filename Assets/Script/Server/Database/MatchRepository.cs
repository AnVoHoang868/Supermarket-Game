#if UNITY_SERVER
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Server.Database
{
    public class MatchRepository
    {
        private SQLiteConnection _db;

        public MatchRepository(SQLiteConnection db)
        {
            _db = db;
        }

        public Match Create(Match match)
        {
            match.StartedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
            _db.Insert(match);
            return match;
        }

        public void EndMatch(string matchId, string winnerId, int duration)
        {
            var match = _db.Table<Match>().FirstOrDefault(m => m.Id == matchId);
            if (match != null)
            {
                match.WinnerId = winnerId;
                match.Duration = duration;
                match.EndedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
                _db.Update(match);
            }
        }

        public Match GetById(string id)
        {
            return _db.Table<Match>().FirstOrDefault(m => m.Id == id);
        }

        public List<Match> GetUserHistory(string userId, int limit = 20)
        {
            return _db.Query<Match>(
                @"SELECT m.* FROM Matches m
                  JOIN MatchScores ms ON m.Id = ms.MatchId
                  WHERE ms.UserId = ?
                  ORDER BY m.EndedAt DESC
                  LIMIT ?",
                userId, limit
            );
        }

        // MatchScore operations
        public void AddScore(MatchScore score)
        {
            _db.Insert(score);
        }

        public List<MatchScore> GetMatchScores(string matchId)
        {
            return _db.Table<MatchScore>()
                .Where(ms => ms.MatchId == matchId)
                .OrderBy(ms => ms.Rank)
                .ToList();
        }

        public List<MatchScore> GetTopPlayers(int limit = 50)
        {
            return _db.Query<MatchScore>(
                @"SELECT UserId, SUM(Score) as Score, COUNT(*) as GamesPlayed
                  FROM MatchScores
                  GROUP BY UserId
                  ORDER BY SUM(Score) DESC
                  LIMIT ?",
                limit
            );
        }
    }
}
#endif