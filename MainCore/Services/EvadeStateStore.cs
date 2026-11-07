using System.Collections.Concurrent;

namespace MainCore.Services
{
    /// <summary>
    /// What the attack evasion of each village is doing, for the task and for the Troops screen.
    /// Kept in memory: after a restart the bot reads the rally point again. Troops that were left with
    /// the ally when the app closed have to be sent back by hand.
    /// </summary>
    [RegisterSingleton<EvadeStateStore>]
    public sealed class EvadeStateStore
    {
        public sealed class State
        {
            /// <summary>The troops reached the ally (the wave was too long to cancel) and wait there to come back.</summary>
            public bool Stationed { get; set; }

            public int TravelSeconds { get; set; }
            public DateTime WaveLast { get; set; }
            public DateTime? SentAt { get; set; }
            public DateTime? HomeAt { get; set; }

            public int Incoming { get; set; }
            public DateTime? NextAttack { get; set; }
            public DateTime? WaveFirst { get; set; }
            public DateTime? WaveEnd { get; set; }
            public int WaveCount { get; set; }

            public DateTime? CheckedAt { get; set; }
            public string Message { get; set; } = "";
            public List<string> History { get; } = [];
        }

        private readonly ConcurrentDictionary<int, State> _states = new();

        public State Get(VillageId villageId) => _states.GetOrAdd(villageId.Value, _ => new State());

        public void Clear(VillageId villageId) => _states.TryRemove(villageId.Value, out _);

        /// <summary>Sets the current message and keeps the last 20 important ones.</summary>
        public void Say(VillageId villageId, string message, bool history = false)
        {
            var state = Get(villageId);
            lock (state)
            {
                state.Message = message;
                if (!history) return;
                state.History.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
                if (state.History.Count > 20) state.History.RemoveAt(state.History.Count - 1);
            }
        }
    }
}
