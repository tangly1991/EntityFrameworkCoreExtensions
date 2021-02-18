using System;
using System.Collections.Generic;
using System.Text;

namespace Ame.EntityFrameworkCore.Extensions
{
    /// <summary>
    /// 查询条件
    /// </summary>
    public class LinqSelectCondition
    {
        /// <summary>
        /// 查询字段名称
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 查询操作类型
        /// </summary>
        public LinqSelectOperator Operator { get; set; }
    }

    public enum LinqSelectOperator
    {
        Contains,
        Equal,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        NotEqual,
        InWithEqual,  // 对于多个值执行等于比较
        InWithContains,// 对于多个值执行包含比较
        Between,
    }
}
