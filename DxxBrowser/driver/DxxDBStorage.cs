using Common;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace DxxBrowser.driver {
    public class DxxDBStorage : IDxxStorageManager, IDxxNGList {
        /**
         * Transactionクラス
         */
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

        #region Singleton

        public static DxxDBStorage Instance { get; private set; }   // = new DxxDBStorage();

        public static void Initialize() {
            Instance = new DxxDBStorage();
        }

        public static void Terminate() {
            Instance?.Dispose();
            Instance = null;
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
                )",
                @"CREATE TABLE IF NOT EXISTS t_ng (
                    id INTEGER NOT NULL PRIMARY KEY,
                    url TEXT NOT NULL UNIQUE,
                    ignore INTEGER NOT NULL DEFAULT '0'
                )"
            );
        }

        public void Dispose() {
            mDB?.Close();
            mDB?.Dispose();
            mDB = null;
        }

        #endregion


        #region Public Properties

        /**
         * ファイルを保存するディレクトリ（DefaultDriverの設定）
         */
        public string StoragePath { get; set; }

        #endregion

        #region Public API's

        /**
         * トランザクションを開始する
         */
        public Txn Transaction() {
            return new Txn(mDB.BeginTransaction());
        }

        /**
         * ダウンロードする IDxxStorageManager i/f
         */
        public void Download(DxxTargetInfo target, Action<bool> onCompleted) {
            Download(target, StoragePath, onCompleted);
        }

        /**
         * ダウンロードする
         * 保存フォルダを指定できるバージョン(DefaultDriver以外で利用）
         */
        public void Download(DxxTargetInfo target, string storagePath, Action<bool> onCompleted) {
            if(storagePath==null) {
                storagePath = StoragePath;
            }

            if (DxxNGList.Instance.IsNG(target.Url)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Dislike ({target.Name})");
                onCompleted?.Invoke(false);
                return;
            }
            var rec = Retrieve(target.Url);
            if (rec != null) {
                if (rec.Status == DLStatus.COMPLETED ||
                     (rec.Status == DLStatus.RESERVED && DxxDownloader.Instance.IsDownloading(rec.Url))) {
                    DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped ({target.Name})");
                    DxxPlayer.PlayList.AddSource(DxxPlayItem.FromTarget(target));
                    onCompleted?.Invoke(false);
                    return;
                }
            } else {
                rec = Reserve(target);
                if (rec == null) {
                    DxxLogger.Instance.Error(LOG_CAT, $"Can't Reserved ({target.Name})");
                    onCompleted?.Invoke(false);
                    return;
                }
            }
            string fileName = createFileName(rec);
            var path = System.IO.Path.Combine(storagePath, fileName);
            DxxDownloader.Instance.Reserve(target, path, DxxDownloader.MAX_RETRY, (r) => {
                if (r) {
                    CompletePath(rec.ID, path);
                    DxxPlayer.PlayList.AddSource(DxxPlayItem.FromTarget(target));
                    DxxLogger.Instance.Success(LOG_CAT, $"Completed: {target.Name}");
                } else {
                    DxxLogger.Instance.Error(LOG_CAT, $"Error: {target.Name}");
                }
                onCompleted?.Invoke(r);
            });
        }

        /**
         * 保存ファイルのパスを取得
         */
        public string GetSavedFile(Uri uri) {
            var rec = Retrieve(uri.ToString());
            if (null == rec) {
                return null;
            }
            return File.Exists(rec.Path) ? rec.Path : null;
        }

        /**
         * ダウンロード済みか？
         */
        public bool IsDownloaded(Uri uri) {
            var rec = Retrieve(uri.ToString());
            return rec?.Status == DLStatus.COMPLETED;
        }


        #endregion

        #region Privates

        private SQLiteConnection mDB;

        public enum DLStatus {
            NONE = 0,
            RESERVED = 1,
            COMPLETED = 2,
            FATAL_ERROR = 3,
        }

        public class DBRecord : MicPropertyChangeNotifier {
            public long ID { get; }
            public string Url { get; }
            public string Name { get; }
            public string Desc { get; }

            private string mPath = null;
            public string Path {
                get => mPath;
                set => setProp(callerName(), ref mPath, value);
            }
            private DLStatus mStatus;
            public DLStatus Status {
                get => mStatus;
                set => setProp(callerName(), ref mStatus, value);
            }
            public DBRecord(long id, string url, string name, string path, string desc, DLStatus status) {
                ID = id;
                Url = url;
                Name = name;
                Path = path;
                Desc = desc;
                Status = status;
            }
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

        public IEnumerable<DBRecord> ListAll() {
            using (var cmd = mDB.CreateCommand()) {
                cmd.CommandText = $"SELECT * FROM t_storage";
                using (var reader = cmd.ExecuteReader()) {
                    if (reader.Read()) {
                        yield return new DBRecord(Convert.ToInt64(reader["id"]),
                            Convert.ToString(reader["url"]),
                            Convert.ToString(reader["name"]),
                            Convert.ToString(reader["path"]),
                            Convert.ToString(reader["desc"]),
                            (DLStatus)Convert.ToInt32(reader["status"]));
                    }
                }
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

        /**
         * FileBasedStorageから登録する（移行用）
         */
        public bool RegisterAsCompleted(DxxTargetInfo target, string path) {
            try {
                var url = target.Url;
                if (string.IsNullOrEmpty(url)) {
                    return false;
                }
                var name = DxxUrl.TrimName(target.Name);
                if (string.IsNullOrEmpty(name)) {
                    name = DxxUrl.TrimName(DxxUrl.GetFileName(url));
                    if (string.IsNullOrEmpty(name)) {
                        name = "untitled";
                    }
                }
                using (var cmd = mDB.CreateCommand()) {
                    cmd.CommandText = $"INSERT INTO t_storage (url,name,path,status,desc) VALUES('{url}','{name}','{path}',{(int)DLStatus.COMPLETED},'{target.Description}')";
                    if (1 == cmd.ExecuteNonQuery()) {
                        return true;
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine(e.StackTrace);
            }
            return false;
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
        #endregion

        #region IDxxNGList i/f

        public bool RegisterNG(string url) {
            try {
                using (var cmd = mDB.CreateCommand()) {
                    cmd.CommandText = $"UPDATE t_ng SET ignore='0' WHERE url='{url}'";
                    if (1 == cmd.ExecuteNonQuery()) {
                        return true;
                    }
                    cmd.CommandText = $"INSERT INTO t_ng (url,ignore) VALUES('{url}','0')";
                    return 1 == cmd.ExecuteNonQuery();
                }
            } catch(Exception e) {
                Debug.WriteLine(e.StackTrace);
                DxxLogger.Instance.Error(LOG_CAT, "RegisterNG Failed.");
                return false;
            }
        }

        public bool UnregisterNG(string url) {
            try {
                using (var cmd = mDB.CreateCommand()) {
                    cmd.CommandText = $"UPDATE t_ng SET ignore='1' WHERE url='{url}'";
                    return true;
                }
            } catch (Exception e) {
                Debug.WriteLine(e.StackTrace);
                DxxLogger.Instance.Error(LOG_CAT, "UnregisterNG Failed.");
                return false;
            }
        }

        public bool IsNG(string url) {
            using (var cmd = mDB.CreateCommand()) {
                try {
                    cmd.CommandText = $"SELECT * FROM t_ng WHERE url='{url}'";
                    using (var reader = cmd.ExecuteReader()) {
                        if (reader.Read()) {
                            return Convert.ToInt64(reader["ignore"]) == 0;
                        }
                    }
                } catch (Exception e) {
                    Debug.WriteLine(e.StackTrace);
                }
                return false;
            }
        }

        #endregion
    }
}
