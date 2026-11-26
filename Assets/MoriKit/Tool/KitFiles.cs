using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 킷 데이터 폴더 = Assets/MoriKit/Data~ (끝의 ~ 때문에 Unity 가 에셋으로 가져오지 않고, 게임 빌드에도 안 들어감).
    ///  · 그림(PSD) · 연출 카드 · 프리팹 연결 · 사슬 · 팔레트 = Data~/Fight
    ///  · 전투 튜닝 · 차트 · 음악 · 설정 · 조작 키 · 시트 내보내기 = Data~ 바로 아래
    /// 킷을 뺄 때는 Assets/MoriKit 하나만 지우면 된다(빼기 전에 ‘MFS 로 내보내기’ · 백업은 프로젝트 밖에 따로 남음).
    /// 처음 한 번: 예전 위치(사용자 데이터 폴더/MoriKit · StreamingAssets/Fight)의 파일을 복사해 옮겨 온다(원래 파일은 그대로 둠).
    /// </summary>
    public static class KitPaths
    {
        static string root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() { root = null; _ = Root; }     // 메인 스레드에서 미리(그림 읽기 스레드가 Application.dataPath 를 부르지 않게)

        public static string Root
        {
            get
            {
                if (root != null) return root;
                root = Path.Combine(Application.dataPath, "MoriKit", "Data~");
                try { Directory.CreateDirectory(root); } catch { }
                Migrate();
                return root;
            }
        }

        public static string Data => Root;
        public static string Art => Path.Combine(Root, "Fight");

        /// <summary>예전 두 위치 → Data~ (같은 이름이 이미 있으면 건너뜀 · 원본은 지우지 않음).</summary>
        static void Migrate()
        {
            string mark = Path.Combine(root, ".moved");
            if (File.Exists(mark)) return;
            int n = 0;
            try { n += CopyTree(Path.Combine(Application.persistentDataPath, "MoriKit"), root); }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 예전 데이터(사용자 폴더) 옮기기 실패: " + e.Message); }
            try { n += CopyTree(Path.Combine(Application.streamingAssetsPath, "Fight"), Path.Combine(root, "Fight")); }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 예전 그림(StreamingAssets/Fight) 옮기기 실패: " + e.Message); }
            try { File.WriteAllText(mark, "Mori 킷 데이터 옮김 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + $" · 파일 {n}개"); } catch { }
            if (n > 0) Debug.Log($"[Mori 킷] 데이터를 {root} 로 모았어요(파일 {n}개 복사 · 예전 위치의 원본은 그대로 — 확인 뒤 지워도 돼요).");
        }

        public static int CopyTree(string src, string dst, bool overwrite = false)
        {
            if (!Directory.Exists(src)) return 0;
            int n = 0;
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string to = Path.Combine(dst, Path.GetFileName(f));
                if (!overwrite && File.Exists(to)) continue;
                File.Copy(f, to, true);
                n++;
            }
            foreach (var d in Directory.GetDirectories(src))
            {
                string name = Path.GetFileName(d);
                if (name.StartsWith(".")) continue;
                n += CopyTree(d, Path.Combine(dst, name), overwrite);
            }
            return n;
        }
    }

    /// <summary>
    /// 자동 백업(프로젝트 밖): 파일을 덮어쓰기 직전에 그 파일을 백업 폴더에 날짜 · 시간을 붙여 남긴다(파일마다 최근 N개).
    /// 기본 위치 = 문서/MoriKitBackup/&lt;프로젝트 이름&gt;. 위치 · 개수는 에디터 설정에 저장(킷 데이터가 사라져도 남음).
    /// OneDrive · 구글 드라이브 같은 동기화 폴더로 바꾸면 PC 가 망가져도 남는다.
    /// </summary>
    public static class KitBackup
    {
        const string PREF_DIR = "MoriKit.BackupDir", PREF_KEEP = "MoriKit.BackupKeep";
        static string dir;
        static int keep = -1;

        public static string Project => new DirectoryInfo(Application.dataPath).Parent?.Name ?? "Project";
        public static string DefaultDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MoriKitBackup", Project);

        public static string Dir
        {
            get
            {
                if (dir == null)
                {
                    dir = UnityEditor.EditorPrefs.GetString(PREF_DIR, "");
                    if (string.IsNullOrEmpty(dir)) dir = DefaultDir;
                }
                return dir;
            }
            set
            {
                dir = string.IsNullOrEmpty(value) ? DefaultDir : value;
                UnityEditor.EditorPrefs.SetString(PREF_DIR, dir == DefaultDir ? "" : dir);
            }
        }

        /// <summary>파일마다 남길 개수(1 ~ 100, 기본 10).</summary>
        public static int Keep
        {
            get { if (keep < 0) keep = Mathf.Clamp(UnityEditor.EditorPrefs.GetInt(PREF_KEEP, 10), 1, 100); return keep; }
            set { keep = Mathf.Clamp(value, 1, 100); UnityEditor.EditorPrefs.SetInt(PREF_KEEP, keep); }
        }

        /// <summary>킷 데이터 안의 경로 → 백업 안의 같은 자리(밖의 파일은 ‘기타’ 아래).</summary>
        static string Rel(string path)
        {
            string full = Path.GetFullPath(path), r = Path.GetFullPath(KitPaths.Root);
            if (full.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return full.Substring(r.Length).TrimStart('\\', '/');
            return Path.Combine("기타", Path.GetFileName(path));
        }

        /// <summary>
        /// 덮어쓰기 · 지우기 직전에 부른다: 지금 파일을 백업에 남김. 실패해도 저장은 막지 않는다.
        ///  · 마지막 백업과 내용이 같으면 건너뜀
        ///  · 자주 저장되는 파일(연출 카드 · 튜닝 · 설정 — 바꿀 때마다 저장)이 백업을 금방 밀어내지 않게, 마지막 백업이 3분 안이면 건너뜀
        ///  · force = 되돌릴 수 없는 작업 직전(캔버스 크기 등) — 언제나 남김
        /// </summary>
        public static void Before(string path, bool force = false)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                string rel = Rel(path);
                string bdir = Path.Combine(Dir, Path.GetDirectoryName(rel) ?? "");
                Directory.CreateDirectory(bdir);
                string name = Path.GetFileNameWithoutExtension(rel), ext = Path.GetExtension(rel);
                var olds = Directory.GetFiles(bdir, name + "__*" + ext).OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (olds.Count > 0)
                {
                    string last = olds[olds.Count - 1];
                    if (!force && (DateTime.Now - File.GetCreationTime(last)).TotalMinutes < 3) return;
                    if (Same(last, path)) return;
                }
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string dst = Path.Combine(bdir, $"{name}__{stamp}{ext}");
                for (int i = 2; File.Exists(dst); i++) dst = Path.Combine(bdir, $"{name}__{stamp}-{i}{ext}");
                File.Copy(path, dst, false);
                olds.Add(dst);
                while (olds.Count > Keep) { try { File.Delete(olds[0]); } catch { } olds.RemoveAt(0); }
            }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 백업 실패(저장은 계속): " + e.Message); }
        }

        static bool Same(string a, string b)
        {
            var fa = new FileInfo(a); var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        /// <summary>킷 데이터 전체를 한 번에(백업/_전체/날짜-시간). 전체 백업은 최근 5개만.</summary>
        public static string All(out int files)
        {
            files = 0;
            string baseDir = Path.Combine(Dir, "_전체");
            string dst = Path.Combine(baseDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            files = KitPaths.CopyTree(KitPaths.Root, dst, true);
            try
            {
                var all = Directory.GetDirectories(baseDir).OrderBy(x => x, StringComparer.Ordinal).ToList();
                while (all.Count > 5) { Directory.Delete(all[0], true); all.RemoveAt(0); }
            }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 오래된 전체 백업 정리 실패: " + e.Message); }
            return dst;
        }

        public static void Open(string path)
        {
            try { Directory.CreateDirectory(path); Application.OpenURL("file:///" + path.Replace('\\', '/')); }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 폴더를 열지 못했어요: " + e.Message); }
        }

        /// <summary>덮어쓰지 않는 새 파일 이름: 있으면 이름_2 · 이름_3 …(확장자 앞).</summary>
        public static string Unique(string dir, string name, params string[] suffixes)
        {
            for (int i = 1; i < 10000; i++)
            {
                string n = i == 1 ? name : $"{name}_{i}";
                bool taken = suffixes.Any(s => File.Exists(Path.Combine(dir, n + s)));
                if (!taken) return n;
            }
            return name + "_" + DateTime.Now.ToString("HHmmss");
        }
    }
}
