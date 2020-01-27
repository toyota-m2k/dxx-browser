using Common;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Windows.Threading;

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

        public static void Initialize(DispatcherObject owner) {
            Instance = new DxxDBStorage(owner);
            Instance.DLPlayList = new DxxDownloadPlayLit(owner);
        }

        public static void Terminate() {
            Instance?.Dispose();
            //Instance = null;
        }

        public DxxDownloadPlayLit DLPlayList { get; private set; }

        private DxxDBStorage(DispatcherObject owner) {
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
                    desc TEXT,
                    driver TEXT,
                    flags INTEGER DEFAULT '0',
                    date INTEGER DEFAULT '0'
                )"
                // ,
                //@"CREATE TABLE IF NOT EXISTS t_ng (
                //    id INTEGER NOT NULL PRIMARY KEY,
                //    url TEXT NOT NULL UNIQUE,
                //    ignore INTEGER NOT NULL DEFAULT '0'
                //)"
            );
            //DLPlayList = new DxxDownloadPlayLit(owner);

            //ConvertFromNGTable();
        }

        public void Dispose() {
            mDB?.Close();
            mDB?.Dispose();
            mDB = null;
            DLPlayList?.Dispose();
            DLPlayList = null;
        }

        #endregion


        #region Events (observing db)

        public enum DBModification {
            APPEND,
            REMOVE,
            UPDATE,
        }
        public delegate void DBUpdatedProc(DBModification type, DBRecord rec);
        public event DBUpdatedProc DBUpdated;

        private void FireDBUpdatedEvent(DBModification type, Func<DBRecord> getRec) {
            DBUpdated?.Invoke(type, getRec());
        }

        #endregion

        #region Public Properties

        /**
         * ファイルを保存するディレクトリ（DefaultDriverの設定）
         */
        //public string StoragePath { get; set; }

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
        //public void Download(DxxTargetInfo target, Action<bool> onCompleted) {
        //    Download(target, StoragePath, onCompleted);
        //}

        /**
         * ダウンロードする
         * 保存フォルダを指定できるバージョン(DefaultDriver以外で利用）
         */
        public void Download(DxxTargetInfo target, IDxxDriver driver, Action<bool> onCompleted) {
            var rec = Retrieve(target.Url);
            string path = null;
            if (rec != null) {
                if (rec.Status == DLStatus.FORBIDDEN || rec.Status==DLStatus.FATAL_ERROR) {
                    DxxLogger.Instance.Cancel(LOG_CAT, $"{rec.Status} ({target.Name})");
                    onCompleted?.Invoke(false);
                    return;
                } else if (rec.Status == DLStatus.COMPLETED ||
                     (rec.Status == DLStatus.RESERVED && DxxDownloader.Instance.IsDownloading(rec.Url))) {
                    if(rec.Description!="サンプル動画" && rec.Description!=target.Description) {
                        UpdateDescription(rec.ID, target.Description);
                    }
                    DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped ({target.Name})");
                    //DxxPlayer.PlayList.AddSource(DxxPlayItem.FromTarget(target));
                    onCompleted?.Invoke(false);
                    return;
                }
                path = rec.Path;
            } else {
                path = driver.ReserveFilePath(target.Uri);
                rec = Reserve(target, driver.Name, path, 0);
                if (rec == null) {
                    DxxLogger.Instance.Error(LOG_CAT, $"Can't Reserved ({target.Name})");
                    onCompleted?.Invoke(false);
                    return;
                }
            }
            if(string.IsNullOrWhiteSpace(path)) {
                string fileName = createFileName(rec);
                path = System.IO.Path.Combine(driver.StoragePath, fileName);
            }
            DxxDownloader.Instance.Reserve(target, path, DxxDownloader.MAX_RETRY, (r) => {
                bool succeeded = false;
                if (r==DxxDownloadingItem.DownloadStatus.Completed) {
                    CompletePath(rec.ID, path);
                    DLPlayList.AddSource(DxxPlayItem.FromTarget(target));
                    DxxLogger.Instance.Success(LOG_CAT, $"Completed: {target.Name}");
                    succeeded = true;
                } else {
                    DxxLogger.Instance.Error(LOG_CAT, $"Error: {target.Name}");
                    if(r==DxxDownloadingItem.DownloadStatus.Error) {
                        RegisterNG(rec.Url, true);
                    }
                }
                onCompleted?.Invoke(succeeded);
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
            FORBIDDEN = 4,
        }

        public class DBRecord : MicPropertyChangeNotifier, IDxxPlayItem {
            public long ID { get; }
            public string Url { get; }

            private string mDriver = null;
            public string Driver {
                get => mDriver;
                set => setProp(callerName(), ref mDriver, value);
            }

            private string mName = null;
            public string Name {
                get => mName;
                set => setProp(callerName(), ref mName, value);
            }

            private string mDescription = null;
            public string Description {
                get => mDescription;
                set => setProp(callerName(), ref mDescription, value);
            }

            private DateTime mDate = DateTime.UtcNow;
            public DateTime Date {
                get => mDate;
                set => setProp(callerName(), ref mDate, value);
            }


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

            private long mFlags = 0;
            public long Flags {
                get => mFlags;
                set => setProp(callerName(), ref mFlags, value);
            }


            public DBRecord(long id, string url, string name, string path, string desc, DLStatus status, string driver, long flags, DateTime time) {
                ID = id;
                Url = url;
                Name = name;
                Path = path;
                Description = desc;
                mStatus = status;
                Driver = driver;
                mFlags = flags;
                Date = time;
            }

            public void CopyFrom(DBRecord src) {
                Name = src.Name;
                Path = src.Path;
                Description = src.Description;
                Status = src.Status;
                Driver = src.Driver;
                Flags = src.Flags;
                Date = src.Date;
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

        private static string AsString(object obj) {
            if (obj != null && obj != DBNull.Value) {
                return Convert.ToString(obj);
            }
            return "";
        }
        private static long AsLong(object obj) {
            if (obj != null && obj != DBNull.Value) {
                return Convert.ToInt64(obj);
            }
            return 0;
        }

        private static readonly DateTime EpochDate = new DateTime(1970, 1, 1, 0, 0, 0);

        public static DateTime AsTime(object obj) {
            if (obj != null && obj != DBNull.Value) {
                return DateTime.FromFileTimeUtc(Convert.ToInt64(obj));
            }
            return EpochDate;
        }

        private static DBRecord RecordFromReader(SQLiteDataReader reader) {
            return new DBRecord(AsLong(reader["id"]),
                AsString(reader["url"]),
                AsString(reader["name"]),
                AsString(reader["path"]),
                AsString(reader["desc"]),
                (DLStatus)AsLong(reader["status"]),
                AsString(reader["driver"]),
                AsLong(reader["flags"]),
                AsTime(reader["date"])
                );
        }

        private DBRecord Retrieve(string url) {
            using (var cmd = mDB.CreateCommand()) {
                try {
                    cmd.CommandText = $"SELECT * FROM t_storage WHERE url='{url}'";
                    using (var reader = cmd.ExecuteReader()) {
                        if (reader.Read()) {
                            return RecordFromReader(reader);
                        }
                    }
                } catch (Exception) {
                    // No Data
                }
                return null;
            }
        }

        private DBRecord Retrieve(long id) {
            using (var cmd = mDB.CreateCommand()) {
                try {
                    cmd.CommandText = $"SELECT * FROM t_storage WHERE id='{id}'";
                    using (var reader = cmd.ExecuteReader()) {
                        if (reader.Read()) {
                            return RecordFromReader(reader);
                        }
                    }
                } catch (Exception) {
                    // No Data
                }
                return null;
            }
        }

        public IEnumerable<DBRecord> ListAll() {
            using (var cmd = mDB.CreateCommand()) {
                cmd.CommandText = $"SELECT * FROM t_storage";
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        yield return RecordFromReader(reader);
                    }
                }
            }
        }

        public IEnumerable<DBRecord> ListForRetry() {
            using (var cmd = mDB.CreateCommand()) {
                cmd.CommandText = $"SELECT * FROM t_storage WHERE status='{(int)DLStatus.RESERVED}'";
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        yield return RecordFromReader(reader);
                    }
                }
            }
        }

        private DBRecord Reserve(DxxTargetInfo target, string driverName, string filePath, int flags=0) {
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
                if(filePath==null) {
                    filePath = "";
                }
                using (var cmd = mDB.CreateCommand()) {
                    cmd.CommandText = $"INSERT INTO t_storage (url,name,path,status,desc,driver,flags) VALUES('{url}','{name}','{filePath}',{(int)DLStatus.RESERVED},'{target.Description}','{driverName}','{flags}')";
                    if (1 == cmd.ExecuteNonQuery()) {
                        var rec = Retrieve(url);
                        FireDBUpdatedEvent(DBModification.APPEND, () => rec);
                        return rec;
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
        public bool RegisterAsCompleted(DxxTargetInfo target, string path, string driverName, int flags=0) {
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
                var info = new FileInfo(path);
                var time = info.CreationTimeUtc.ToFileTimeUtc();

                using (var cmd = mDB.CreateCommand()) {
                    cmd.CommandText = $"UPDATE t_storage SET path='{path}',date='{time}', status={(int)DLStatus.COMPLETED}, desc='{target.Description}', driver='{driverName}', flags='{flags}', date='{time}' WHERE url='{url}'";
                    if (1 == cmd.ExecuteNonQuery()) {
                        FireDBUpdatedEvent(DBModification.UPDATE, () => Retrieve(url));
                        return true;
                    }
                    cmd.CommandText = $"INSERT INTO t_storage (url,name,path,status,desc,driver,flags,date) VALUES('{url}','{name}','{path}',{(int)DLStatus.COMPLETED},'{target.Description}','{driverName}','{flags}','{time}')";
                    if (1 == cmd.ExecuteNonQuery()) {
                        FireDBUpdatedEvent(DBModification.APPEND, () => Retrieve(url));
                        return true;
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine(e.StackTrace);
            }
            return false;
        }

        public bool UpdateFlags(long id, long flags) {
            using (var cmd = mDB.CreateCommand()) {
                cmd.CommandText = $"UPDATE t_storage SET flags='{flags}' WHERE id={id}";
                if (1 == cmd.ExecuteNonQuery()) {
                    FireDBUpdatedEvent(DBModification.UPDATE, () => Retrieve(id));
                    return true;
                }
            }
            return false;
        }

        public bool UpdateDescription(long id, string desc) {
            using (var cmd = mDB.CreateCommand()) {
                cmd.CommandText = $"UPDATE t_storage SET desc='{desc}' WHERE id={id}";
                if (1 == cmd.ExecuteNonQuery()) {
                    FireDBUpdatedEvent(DBModification.UPDATE, () => Retrieve(id));
                    return true;
                }
            }
            return false;
        }

        public bool UpdateTimestamp(long id, string path) {
            try {
                var info = new FileInfo(path);
                var time = info.CreationTimeUtc.ToFileTimeUtc();

                using (var cmd = mDB.CreateCommand()) {
                    cmd.CommandText = $"UPDATE t_storage SET date='{time}' WHERE id={id}";
                    if (1 == cmd.ExecuteNonQuery()) {
                        FireDBUpdatedEvent(DBModification.UPDATE, () => Retrieve(id));
                        return true;
                    }
                }
            } catch(Exception) {

            }
            return false;
        }

        //public bool ComplementRecord(long id, string driver, long flags) {
        //    using (var cmd = mDB.CreateCommand()) {
        //        cmd.CommandText = $"UPDATE t_storage SET flags='{flags}', driver='{driver}' WHERE id={id}";
        //        if (1 == cmd.ExecuteNonQuery()) {
        //            return true;
        //        }
        //    }
        //    return false;
        //}

        protected virtual string LOG_CAT => "DBS";

        private bool CompletePath(long id, string path) {
            using (var cmd = mDB.CreateCommand()) {
                var info = new FileInfo(path);
                var time = info.CreationTimeUtc.ToFileTimeUtc();

                cmd.CommandText = $"UPDATE t_storage SET path='{path}',date='{time}', status={(int)DLStatus.COMPLETED} WHERE id={id}";
                if (1 == cmd.ExecuteNonQuery()) {
                    FireDBUpdatedEvent(DBModification.UPDATE, () => Retrieve(id));
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

        public event PlayItemRemovingHandler PlayItemRemoving;

        public bool RegisterNG(string url, bool fatalError) {
            try {
                using (var cmd = mDB.CreateCommand()) {
                    PlayItemRemoving?.Invoke(url);
                    int status = (int)(fatalError ? DLStatus.FATAL_ERROR : DLStatus.FORBIDDEN);
                    cmd.CommandText = $"UPDATE t_storage SET status='{status}' WHERE url='{url}'";
                    if(1 == cmd.ExecuteNonQuery()) {
                        var rec = Retrieve(url);
                        try {
                            File.Delete(rec.Path);
                        } catch (Exception e) {
                            Debug.WriteLine(e);
                        }
                        FireDBUpdatedEvent(DBModification.UPDATE, () => rec);
                        return true;
                    }
                }
            } catch(Exception e) {
                Debug.WriteLine(e.StackTrace);
                DxxLogger.Instance.Error(LOG_CAT, "RegisterNG Failed.");
            }
            return false;
        }

        public int UnregisterNG(IEnumerable<string> urls) {
            int count = 0;
            if (!Utils.IsNullOrEmpty(urls)) {
                using (Transaction()) {
                    foreach (var url in urls) {
                        using (var cmd = mDB.CreateCommand()) {
                            try {
                                cmd.CommandText = $"UPDATE t_storage SET status='{(int)DLStatus.RESERVED}' WHERE url='{url}'";
                                if (1 == cmd.ExecuteNonQuery()) {
                                    FireDBUpdatedEvent(DBModification.UPDATE, () => Retrieve(url));
                                    count++;
                                }
                            } catch(Exception e) {
                                Debug.WriteLine(e);
                            }
                        }
                    }
                }
            }
            return count;
        }

        //public bool UnregisterNG(string url) {
        //    try {
        //        using (var cmd = mDB.CreateCommand()) {
        //            cmd.CommandText = $"UPDATE t_storage SET status='{(int)DLStatus.RESERVED}' WHERE url='{url}'";
        //            if(1 == cmd.ExecuteNonQuery()) {
        //                var rec = Retrieve(url);
        //                FireDBUpdatedEvent(DBModification.UPDATE, () => rec);
        //                DLPlayList.AddSource(rec);
        //                return true;
        //            }
        //        }
        //    } catch (Exception e) {
        //        Debug.WriteLine(e.StackTrace);
        //        DxxLogger.Instance.Error(LOG_CAT, "UnregisterNG Failed.");
        //    }
        //    return false;
        //}

        //public bool IsNG(string url) {
        //    using (var cmd = mDB.CreateCommand()) {
        //        try {
        //            cmd.CommandText = $"SELECT * FROM t_ng WHERE url='{url}'";
        //            using (var reader = cmd.ExecuteReader()) {
        //                if (reader.Read()) {
        //                    return AsLong(reader["ignore"]) == 0;
        //                }
        //            }
        //        } catch (Exception e) {
        //            Debug.WriteLine(e.StackTrace);
        //        }
        //        return false;
        //    }
        //}

        //public bool ConvertFromNGTable() {
        //    using (Transaction())
        //    using (var cmd = mDB.CreateCommand()) {
        //        try {
        //            cmd.CommandText = $"SELECT * FROM t_ng";
        //            using (var reader = cmd.ExecuteReader()) {
        //                while (reader.Read()) {
        //                    if (AsLong(reader["ignore"]) == 0) {
        //                        RegisterNG(AsString(reader["url"]), false);
        //                    }
        //                }
        //            }
        //        } catch (Exception e) {
        //            Debug.WriteLine(e.StackTrace);
        //        }
        //        return false;
        //    }
        //}

        #endregion
    }
}
