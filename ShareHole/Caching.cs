using ImageMagick;
using System.Collections.Concurrent;
using System.ComponentModel.Design;

namespace ShareHole {
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

    public static class CacheCancellation {
        internal static CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
        internal static CancellationToken cancellation_token => cancellation_token_source.Token;
    }

    public class ConcurrentCache<T> {
        static readonly double max_age = 86400 / 4; // 6 hours

        Type type;
        ConcurrentDictionary<string, (cache_item_life life, T item)> cache = new ConcurrentDictionary<string, (cache_item_life life, T item)>();

        public bool currently_pruning = false;

        public bool Test(string key) => cache.ContainsKey(key);
        public void Remove(string key) => cache.TryRemove(key, out _);

        public void Clear() => cache.Clear();        

        public ConcurrentCache() {
            type = typeof(T);
            StartPruning();
        }

        public void Store(string key, T item) {
            if (item == null) return;
            if (!item.GetType().IsAssignableFrom(type)) return;
            if (Test(key)) return;
            if (cache.TryAdd(key, (new cache_item_life(max_age), item))) {
                Logging.Message($"Stored {item.ToString()} in cache");
            } else {
                Logging.Error($"Failed cache store on {item.ToString()}");
            }
        }
        public void Update(string key, T item) {
            if (item == null) return;
            if (!item.GetType().IsAssignableFrom(type)) return;
            cache.AddOrUpdate(key, (new cache_item_life(max_age), item), (key, old) => (new cache_item_life(), item));
        }
        public T Request(string key) {
            return cache[key].item;
        }

        public void StartPruning() {
            State.task_start(Prune, CacheCancellation.cancellation_token)
                .ContinueWith(a => { currently_pruning = false; });
        }

        private void Prune() {
            currently_pruning = true;

            while (currently_pruning && !State.cancellation_token.IsCancellationRequested) {
            restart:
                if (cache.Keys.Count > 0)
                foreach (var key in cache.Keys.ToList()) {
                    if (cache[key].life.needs_prune()) {
                        cache.TryRemove(key, out _);
                        Logging.ThreadMessage($"Pruned {key} from cache", "Cache", 5);
                        goto restart;
                    }
                }

                Task.Delay(1000);
            }
        }
    }

    public class Cache<T> {
        static readonly double max_age = 86400 / 4;

        Dictionary<string, (cache_item_life life, T item)> cache = new Dictionary<string, (cache_item_life life, T item)>();

        public void Clear() => cache.Clear();

        public bool currently_pruning = false;

        public void Store(string key, T item) {            
            if (Test(key)) return;
            cache.Add(key, (new cache_item_life(max_age), item));
        }

        public bool Test(string key) => cache.ContainsKey(key);
        public void Remove(string key) => cache.Remove(key);

        public Cache(bool enable_pruning = true) {
            if (!typeof(ICacheStruct).IsAssignableFrom(typeof(T)))
                throw new Exception("Not an ICacheStruct");

            if (enable_pruning) StartPruning();
        }

        public T Request(string key) {
            return cache[key].item;
        }

        public void StartPruning() {
            State.task_start(Prune, CacheCancellation.cancellation_token)
                .ContinueWith(a => { currently_pruning = false; });
        }

        private void Prune() {
            currently_pruning = true;

            while (currently_pruning && !State.cancellation_token.IsCancellationRequested) {
            restart:
                foreach (var key in cache.Keys) {
                    if (cache[key].life.needs_prune()) {
                        cache.Remove(key);
                        Logging.ThreadMessage($"Pruned {key} from cache", "Cache", 5);
                        goto restart;
                    }
                }

                Thread.Sleep(1000);
            }
        }
    }

    public interface ICacheStruct {
        public cache_item_life life { get; set; }
        public bool needs_prune() => life.needs_prune();
        public void refresh() => life.refresh();

        public void init(double life_time) {
            life = new cache_item_life(life_time);
        }
    }
    public class MusicCacheItem : ICacheStruct {
        cache_item_life ICacheStruct.life { get; set; }

        internal string filename;
        string mime;

        public string title;
        public string artist;
        public string album;

        public MagickImage cover;


        public MusicCacheItem(string filename, string mime, double life_time=43200.00) {
            ((ICacheStruct)this).init(life_time);
        }

    }
    public class ByteArrayCacheItem : ICacheStruct {
        cache_item_life ICacheStruct.life { get; set; }

        string filename;
        internal string mime;

        internal byte[] data;
        internal int length => data.Length;


        cache_item_life life;

        internal ByteArrayCacheItem(double life_time, string mime) {
            ((ICacheStruct)this).init(life_time);
            this.mime = mime;
        }
    }


}
