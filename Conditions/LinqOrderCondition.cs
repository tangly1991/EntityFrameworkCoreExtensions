using System;
using System.Collections.Generic;
using System.Text;

namespace Ame.EntityFrameworkCore.Extensions
{
    /// <summary>
    /// 排序
    /// </summary>
    public class LinqOrderCondition
    {
        public LinqOrderCondition()
        {
            ParentFields = new List<string>();
        }
        /// <summary>
        /// 查询字段名称
        /// </summary>
        public string Field { get; set; }
        /// <summary>
        /// 排序类型
        /// </summary>
        public LinqOrderType OrderType { get; set; }
        /// <summary>
        /// 父表字段集合，按先后次序，层次依次升高
        /// </summary>
        public List<string> ParentFields { get; set; }
    }

    public enum LinqOrderType
    {
        ASC,
        DESC,
    }
}
