using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace TodoTool
{
    /// <summary>
    /// 数据持久化：%APPDATA%\TodoTool\data.json。
    /// 存储格式与原 HTML 版内嵌 JSON 完全互通：
    /// {updatedAt, items:[{id,text,note,owner,deadline,deadlineText,done}], checked:{"1":true}, sortByDeadline}
    /// 因此 data.json 的 items/checked 块可以直接拷回 HTML 文件的 todoData 标签，反之亦然。
    /// </summary>
    public class TodoStore
    {
        public List<TodoItem> Items = new List<TodoItem>();
        public bool SortByDeadline = false;
        public long UpdatedAt = 0;

        public readonly string DataDir;
        public readonly string DataFile;

        public TodoStore()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            DataDir = Path.Combine(baseDir, "TodoTool");
            DataFile = Path.Combine(DataDir, "data.json");
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(DataFile)) return;
                string json = File.ReadAllText(DataFile, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return;
                Parse(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TodoStore.Load failed: " + ex.Message);
            }
        }

        private void Parse(string json)
        {
            object rootObj = MiniJson.Parse(json);
            Dictionary<string, object> root = rootObj as Dictionary<string, object>;
            if (root == null) return;

            SortByDeadline = GetBool(root, "sortByDeadline");
            UpdatedAt = GetLong(root, "updatedAt");

            List<object> arr = root.ContainsKey("items") ? root["items"] as List<object> : null;
            if (arr == null) return;

            Items.Clear();
            foreach (object o in arr)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null) continue;
                TodoItem it = new TodoItem();
                it.Id = (int)GetLong(d, "id");
                it.Text = GetString(d, "text");
                it.Note = GetString(d, "note");
                it.Owner = GetString(d, "owner");
                it.Deadline = GetString(d, "deadline");
                it.DeadlineText = GetString(d, "deadlineText");
                if (d.ContainsKey("done") && d["done"] is bool) it.Done = (bool)d["done"];
                Items.Add(it);
            }

            // 兼容原版格式：checked 为独立 {id:true} 字典
            Dictionary<string, object> checkedMap = root.ContainsKey("checked")
                ? root["checked"] as Dictionary<string, object> : null;
            if (checkedMap != null)
            {
                foreach (KeyValuePair<string, object> kv in checkedMap)
                {
                    int id;
                    if (!int.TryParse(kv.Key, out id)) continue;
                    if (!(kv.Value is bool) || !(bool)kv.Value) continue;
                    TodoItem target = Items.Find(delegate (TodoItem x) { return x.Id == id; });
                    if (target != null) target.Done = true;
                }
            }
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v)) return v as string ?? "";
            return "";
        }

        private static long GetLong(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return 0;
            if (v is long) return (long)v;
            if (v is double) return (long)(double)v;
            if (v is int) return (int)v;
            long r;
            return long.TryParse(v.ToString(), out r) ? r : 0;
        }

        private static bool GetBool(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v)) return false;
            if (v is bool) return (bool)v;
            return v != null && (v.ToString() == "1" || v.ToString().Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>立即写盘（原子写：.tmp + 替换，避免写一半损坏）。</summary>
        public void Save()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                UpdatedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                string json = BuildJson();
                string tmp = DataFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(DataFile)) File.Replace(tmp, DataFile, DataFile + ".bak");
                else File.Move(tmp, DataFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TodoStore.Save failed: " + ex.Message);
            }
        }

        public int NextId()
        {
            int max = 0;
            foreach (TodoItem it in Items) if (it.Id > max) max = it.Id;
            return max + 1;
        }

        private string BuildJson()
        {
            StringBuilder sb = new StringBuilder(512 + Items.Count * 160);
            sb.Append("{\"updatedAt\":").Append(UpdatedAt.ToString(CultureInfo.InvariantCulture)).Append(",\"items\":[");
            List<string> checkedPairs = new List<string>();
            for (int i = 0; i < Items.Count; i++)
            {
                TodoItem it = Items[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(it.Id.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"text\":\"").Append(MiniJson.Escape(it.Text)).Append('"');
                sb.Append(",\"note\":\"").Append(MiniJson.Escape(it.Note)).Append('"');
                sb.Append(",\"owner\":\"").Append(MiniJson.Escape(it.Owner)).Append('"');
                sb.Append(",\"deadline\":\"").Append(MiniJson.Escape(it.Deadline)).Append('"');
                sb.Append(",\"deadlineText\":\"").Append(MiniJson.Escape(it.DeadlineText)).Append('"');
                sb.Append(",\"done\":").Append(it.Done ? "true" : "false");
                sb.Append('}');
                if (it.Done) checkedPairs.Add("\"" + it.Id + "\":true");
            }
            sb.Append("],\"checked\":{").Append(string.Join(",", checkedPairs.ToArray()));
            sb.Append("},\"sortByDeadline\":").Append(SortByDeadline ? "true" : "false").Append('}');
            return sb.ToString();
        }
    }
}
