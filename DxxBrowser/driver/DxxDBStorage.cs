using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public class DxxDBStorage : IDxxStorageManager {
        public class Txn : IDisposable {
            private SQLiteTransaction mTxn;

            internal Txn(SQLiteTransaction txn) {
                mTxn = txn;
            }

            public void Commit() {
                Dispose();
            }

            public void Rollback() {
                mTxn?.Rollback();
                mTxn?.Dispose();
                mTxn = null;
            }

            public void Dispose() {
                mTxn?.Commit();
                mTxn?.Dispose();
                mTxn = null;
            }
        }

        public static DxxDBStorage Instance { get; } = new DxxDBStorage();

        public string StoragePath { get; set; }
        private SQLiteConnection mDB;

        private enum DLStatus {
            NONE = 0,
            RESERVED = 1,
            COMPLETED = 2,
        }

        class DBRecord {
            public long ID { get; }
            public string Url { get; }
            public string Name { get;}
            public string Path { get;}
            public string Desc { get;}
            public DLStatus Status { get; }
            public DBRecord(long id, string url, string name, string path, string desc, DLStatus status) {
                ID = id;
                Url = url;
                Name = name;
                Path = path;
                Desc = desc;
                Status = status;
            }
        }

        private DxxDBStorage() {
            var builder = new SQLiteConnectionStringBuilder() { DataSource = "dxxStorage.db" };
            mDB = new SQLiteConnection(builder.ToString());
            mDB.Open();
            executeSql(
                @"CREATE TABLE IF NOT EXISTS t_storage (
                    id INTEGER NOT NULL PRIMARY KEY,
                    url TEXT NOT NULL UNIQUE,
                    name TEXT NOT NULL,
                    path TEXT NOT NULL,
                    status INTEGER NOT NULL,
                    desc TEXT
                )"
            );
        }

        public void Dispose() {
            mDB?.Close();
            mDB?.Dispose();
            mDB = null;
        }

        public Txn Transaction() {
            return new Txn(mDB.BeginTransaction());
        }

        private void executeSql(params string[] sqls) {
            using (var cmd = mDB.CreateCommand()) {
                foreach (var sql in sqls) {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private DBRecord Retrieve(string url) {
            using (var cmd = mDB.CreateCommand()) {
                try {

                    cmd.CommandText = $"SELECT * FROM t_storage WHERE url='{url}'";
                    using (var reader = cmd.ExecuteReader()) {
                        if (reader.Read()) {
                            return new DBRecord(Convert.ToInt64(reader["id"]),
                                Convert.ToString(reader["url"]),
                                Convert.ToString(reader["name"]),
                                Convert.ToString(reader["path"]),
                                Convert.ToString(reader["desc"]),
                                (DLStatus)Convert.ToInt32(reader["status"]));
                        }
                    }
                } catch (Exception) {
                }
                return null;
            }
        }

        private DBRecord Reserve(DxxTargetInfo target) {
            try {
                var url = target.Url;
                if (string.IsNullOrEmpty(url)) {
                    return null;
                }
                var name = DxxUrl.TrimName(target.Name);
                if (string.IsNullOrEmpty(name)) {
                    name = DxxUrl.TrimName(DxxUrl.GetFileName(url));
                    if (string.IsNullOrEmpty(name)) {
                        name = "untitled";
                    }
                }
                using (var cmd = mDB.CreateCommand()) {
                    cmd.CommandText = $"INSERT INTO t_storage (url,name,path,status,desc) VALUES('{url}','{name}','',{(int)DLStatus.RESERVED},'{target.Description}')";
                    if (1 == cmd.ExecuteNonQuery()) {
                        return Retrieve(url);
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine(e.StackTrace);
            }
            return null;
        }

        protected virtual string LOG_CAT => "DBS";

        private bool CompletePath(long id, string path) {
            using (var cmd = mDB.CreateCommand()) {
                cmd.CommandText = $"UPDATE t_storage SET path='{path}', status={(int)DLStatus.COMPLETED} WHERE id={id}";
                if (1 == cmd.ExecuteNonQuery()) {
                    return true;
                }
            }
            Debug.WriteLine("RegisterPath error.");
            DxxLogger.Instance.Comment(LOG_CAT, "Complete path error.");
            return false;
        }

        private string createFileName(DBRecord rec) {
            var uri = new Uri(rec.Url);
            var ext = System.IO.Path.GetExtension(rec.Name) ?? "";
            var name = System.IO.Path.GetFileNameWithoutExtension(rec.Name) ?? "noname";
            return $"{name}-{rec.ID}{ext}";
        }

        public void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
            var rec = Retrieve(target.Url);
            if(rec!=null) {
                if ( rec.Status == DLStatus.COMPLETED || 
                     (rec.Status == DLStatus.RESERVED && DxxDownloader.Instance.IsDownloading(rec.Url))) {
                    DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped ({target.Name})");
                    DxxPlayer.PlayList.AddSource(rec.Path);
                    return;
                }
            } else { 
                rec = Reserve(target);
                if (rec == null) {
                    DxxLogger.Instance.Error(LOG_CAT, $"Can't Reserved ({target.Name})");
                    return;
                }
            }
            string fileName = createFileName(rec);
            var path = System.IO.Path.Combine(StoragePath, fileName);
            DxxDownloader.Instance.Download(target, path, (r) => {
                if (r) {
                    CompletePath(rec.ID, path);
                    DxxPlayer.PlayList.AddSource(path);
                    DxxLogger.Instance.Success(LOG_CAT, $"Completed: {target.Name}");
                } else {
                    DxxLogger.Instance.Error(LOG_CAT, $"Error: {target.Name}");
                }
                onCompleted?.Invoke(r);
            });
        }

        public string GetSavedFile(Uri uri) {
            var rec = Retrieve(uri.ToString());
            return rec?.Path;
        }

        public bool IsDownloaded(Uri uri) {
            var rec = Retrieve(uri.ToString());
            return rec?.Status == DLStatus.COMPLETED;
        }


    }
}
