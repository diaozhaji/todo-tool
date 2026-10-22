namespace TodoTool
{
    /// <summary>一条待办项。字段与原 HTML 版数据格式一一对应（id/text/note/owner/deadline/deadlineText）。</summary>
    public class TodoItem
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string Note { get; set; }
        public string Owner { get; set; }
        /// <summary>标准 ISO 日期 yyyy-MM-dd；为空表示无有效日期。</summary>
        public string Deadline { get; set; }
        /// <summary>无法解析为日期时的原文（原样展示）。</summary>
        public string DeadlineText { get; set; }
        public bool Done { get; set; }

        public TodoItem()
        {
            Text = "";
            Note = "";
            Owner = "";
            Deadline = "";
            DeadlineText = "";
        }

        public TodoItem Clone()
        {
            return (TodoItem)MemberwiseClone();
        }
    }
}
