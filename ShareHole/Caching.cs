﻿using ImageMagick;
using System.Collections.Concurrent;
using System.ComponentModel.Design;

namespace ShareHole {
    public static class CacheCancellation {
        internal static CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
        internal static CancellationToken cancellation_token => cancellation_token_source.Token;

        public static void Cancel() => cancellation_token_source.Cancel(true);        
        public static void Reset() => cancellation_token_source = new CancellationTokenSource();
    }

    public struct cache_item_life {
        internal double birth_time; internal double life_time = 0;

        public cache_item_life(double life_time) {
            this.life_time = life_time;
            refresh();
        }

        public double age => DateTimeOffset.UtcNow.ToUnixTimeSeconds() - birth_time;
        public void refresh() => birth_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public bool needs_prune() => age > life_time;
    }

    public class ConcurrentCache<T> {
        public readonly double MaxAge = 86400; // 1 day

        Type type;
        ConcurrentDictionary<string, (cache_item_life life, T item)> cache = new ConcurrentDictionary<string, (cache_item_life life, T item)>();

        bool currently_pruning = false;

        public bool Test(string key) => cache.ContainsKey(key);
        public void Remove(string key) => cache.TryRemove(key, out _);

        public void Clear() => cache.Clear();        

        public ConcurrentCache() {
            type = typeof(T);
            StartPruning();
        }

        public ConcurrentCache(double age_seconds) {
            MaxAge = age_seconds;
            type = typeof(T);
            StartPruning();
        }

        ~ConcurrentCache() {
            currently_pruning = false;            
        }

        public void Store(string key, T item) {
            Store(key, item, MaxAge);
        }

        public void Store(string key, T item, double life_time) {
            if (item == null) return;
            if (!item.GetType().IsAssignableFrom(type)) return;
            if (Test(key)) return;
            if (cache.TryAdd(key, (new cache_item_life(life_time), item))) {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.Message($"Stored {key}::{item.ToString()} in cache for {life_time} seconds");
            } else {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.Error($"Failed cache store on {key}::{item.ToString()}");
            }
        }

        public void Update(string key, T item) {
            if (item == null) return;
            if (!item.GetType().IsAssignableFrom(type)) return;
            cache.AddOrUpdate(key, (new cache_item_life(MaxAge), item), (key, old) => (new cache_item_life(), item));
        }
        public T Request(string key) {
            return cache[key].item;
        }

        public int Count => cache.Count;

        public void StartPruning() {
            State.StartTask(Prune, CacheCancellation.cancellation_token)
                .ContinueWith(a => { currently_pruning = false; });
        }

        private async void Prune() {
            currently_pruning = true;
            
            while (currently_pruning && !State.cancellation_token.IsCancellationRequested) {
            restart:
                if (cache.Keys.Count > 0) {
                    foreach (var key in cache.Keys.ToList()) {
                        if (cache[key].life.life_time != -1 && cache[key].life.needs_prune()) {
                            cache.TryRemove(key, out _);
                            if (State.LogLevel == Logging.LogLevel.ALL)
                                Logging.ThreadMessage($"Pruned {key}::{cache[key].item.ToString()} from cache", "Cache", 5);
                            goto restart;
                        }
                    }
                }
                await Task.Delay(1000);
            }

            currently_pruning = false;
        }
    }
}
