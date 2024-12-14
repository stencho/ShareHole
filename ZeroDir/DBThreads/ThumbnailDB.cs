using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir.DBThreads {
    public class ThumbnailDBRequest {
        public FileInfo file;
        public Image? thumbnail = null;        
        public bool thumbnail_ready = false;
                
        public ThumbnailDBRequest(FileInfo file) { 
            this.file = file;
        }
    }

    public class ThumbnailDB {
        //thread for handling thumbnail requests
        Thread request_thread;

        //threads for building thumbnails
        Thread[] build_threads;
        int build_thread_count = 16;

        Queue<ThumbnailDBRequest> request_queue = new Queue<ThumbnailDBRequest>();

        public Image? RequestThumbnail(string filename) {
            FileInfo f = new FileInfo(filename);
            if (f.Exists) {
                return RequestThumbnail(f);
            } else return null;
        }
        public Image? RequestThumbnail(FileInfo file) {

            return null;
        }

        public ThumbnailDB() {
            request_thread = new Thread(handle_requests);
        }

        void handle_requests() {

        }

        void build_thumbnail(ThumbnailDBRequest request) {

        }
    }
}
