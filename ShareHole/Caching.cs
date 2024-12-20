﻿using ImageMagick;

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


    public class Cache {
        static readonly double max_age = 86400 / 4;

        Dictionary<string, ICacheStruct> cache = new Dictionary<string, ICacheStruct>();

        public bool currently_pruning = false;

        public void Store(string key, ICacheStruct item) {
            if (Test(key)) return;
            cache.Add(key, item);
            Logging.ThreadMessage($"Stored {key} in cache", "Cache", 5);
        }

        public bool Test(string key) => cache.ContainsKey(key);
        public void Remove(string key) => cache.Remove(key);

        public Cache(bool enable_pruning = true) {
            if (enable_pruning)
                StartPruning(CurrentConfig.cancellation_token);
        }

        public void StartPruning(CancellationToken cancellation_token) {
            Task.Run(() => {
                currently_pruning = true;
                Logging.ThreadMessage($"Started pruning thread", "Cache", 5);
                
                while (currently_pruning && !CurrentConfig.cancellation_token.IsCancellationRequested) {
                    Prune();
                }

            }, cancellation_token).ContinueWith(a => {
                currently_pruning = false;
            }, cancellation_token);
        }

        private void Prune() {
        restart:
            foreach (var key in cache.Keys) {
                if (cache[key].needs_prune()) {
                    cache.Remove(key);
                    Logging.ThreadMessage($"Pruned {key} from cache", "Cache", 5);
                    goto restart;
                }
            }

            Thread.Sleep(1000);
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